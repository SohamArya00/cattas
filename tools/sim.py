"""Headless copy of the Cattas cat rules (mirrors Cat.cs) for checking levels are solvable.
Usage: python3 tools/sim.py   (runs the built-in level checks)"""
import os, math

CS = 8
class Level:
    def __init__(self, n):
        name = n if isinstance(n, str) else f"level{n:02d}"
        path = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Levels", f"{name}.txt")
        txt = open(path).read().split("\n")
        self.meta = {}; rows = []; inmap = False
        for line in txt:
            if inmap: rows.append(line); continue
            s = line.strip()
            if not s or s.startswith("//"): continue
            if s.lower() == "map:": inmap = True; continue
            k, _, v = s.partition(":"); self.meta[k.strip().lower()] = v.strip()
        while rows and not rows[-1].strip(): rows.pop()
        self.R = len(rows); self.C = max([len(r) for r in rows] or [0])
        self.W, self.H = self.C * CS, self.R * CS
        if "size" in self.meta:
            w, h = self.meta["size"].replace(",", " ").split()[:2]; self.W, self.H = int(w), int(h)
        self.ax0, self.ay0, self.ax1, self.ay1 = 0, 0, self.W, self.H       # play area
        if "area" in self.meta:
            a = [int(v) for v in self.meta["area"].replace(",", " ").split()[:4]]
            self.ax0, self.ay0, self.ax1, self.ay1 = min(a[0], a[2]), min(a[1], a[3]), max(a[0], a[2]), max(a[1], a[3])
        self.objects = []   # [type, x0, y0, x1, y1, extra, on]
        for line in txt:
            st = line.strip()
            if st.startswith("object:"):
                parts = st[7:].split()
                self.objects.append([parts[0], int(parts[1]), int(parts[2]), int(parts[3]), int(parts[4]), parts[5:] , True])
        self.solid = bytearray(self.W * self.H); self.hard = bytearray(self.W * self.H)   # solid: 1 back, 2 front, 3 both
        self.plane = 7          # the lane of the cat being simulated (set by Cat.tick); 1 back, 2 front, 4 mid, 7 all
        self.lanejump = self.meta.get('lanejump', '').lower() in ('on', 'yes')
        sl = self.meta.get('startlane', '').lower()
        self.startlane = {'b': 1, 'back': 1, 'f': 2, 'front': 2, 'm': 4, 'mid': 4}.get(sl, 0)
        self.builtlane = bytearray(self.W * self.H)
        self.built = bytearray(self.W * self.H)   # hairball bridge pixels
        self.front_y = int(self.meta.get("frontplane", 0) or 0)
        self.hatches, self.exits = [], []
        self.paths, self.railblocks = [], []     # path: dict(name, cx, cy, a, b, closed, d0, d1)
        goals = []
        for line in txt:
            st = line.strip()
            if st.startswith("hatch:"):
                p = st[6:].replace(",", " ").split(); self.hatches.append((int(p[0]), int(p[1]), int(p[2]) if len(p) > 2 else 1))
            if st.startswith("path:"):
                q = st[5:].split()
                closed = q[5].lower() != "open"; nxt = 6 if closed or len(q) < 8 else 8
                lane = lambda t: {'b': 1, 'back': 1, 'f': 2, 'front': 2, 'm': 4, 'mid': 4}.get(t.lower(), 0)
                self.paths.append(dict(name=q[0], cx=float(q[1]), cy=float(q[2]), a=float(q[3]), b=float(q[4]), closed=closed,
                                       d0=math.radians(float(q[6])) if (not closed and len(q) > 7) else 0.0, d1=math.radians(float(q[7])) if (not closed and len(q) > 7) else 2 * math.pi,
                                       fl=lane(q[nxt]) if len(q) > nxt + 1 else 0, bl=lane(q[nxt + 1]) if len(q) > nxt + 1 else 0, slide="slide" in [t.lower() for t in q[nxt:]]))
            if st.startswith("railblock:"):
                q = st[10:].split(); self.railblocks.append((int(q[0]), int(q[1]), int(q[2]), int(q[3]), q[4] if len(q) > 4 else "", q[5].lower() if len(q) > 5 else ""))
            if st.startswith("goal:"):
                p = [int(v) for v in st[5:].replace(",", " ").split()[:4]]
                goals.append((min(p[0], p[2]), min(p[1], p[3]), abs(p[2] - p[0]), abs(p[3] - p[1])))
        coll = self.meta.get("collision", "")
        if coll:
            from PIL import Image
            img = Image.open(os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Art", coll + ".bytes")).convert("RGBA")
            px = img.load()
            for y in range(self.H):
                sy = min(img.height - 1, int((y + 0.5) * img.height / self.H))
                for x in range(self.W):
                    r, g, b, a = px[min(img.width - 1, int((x + 0.5) * img.width / self.W)), img.height - 1 - sy]
                    if a > 127:
                        m = (1 if r > 127 else 0) | (2 if g > 127 else 0) | (4 if b > 127 else 0)
                        self.solid[y * self.W + x] = m or 7
        art = self.meta.get("foreground", "")
        if art:
            from PIL import Image
            img = Image.open(os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Art", art + ".bytes")).convert("RGBA")
            k = img.width // self.W; a = img.split()[3].load()
            for y in range(self.H):
                for x in range(self.W):
                    if a[x * k + k // 2, img.height - 1 - (y * k + k // 2)] > 127: self.solid[y * self.W + x] = 7
        stone = self.meta.get("stone", "")
        if (art or coll) and stone:
            from PIL import Image
            img = Image.open(os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Art", stone + ".bytes")).convert("RGBA")
            a = img.split()[3].load()
            for y in range(self.H):
                sy = min(img.height - 1, int((y + 0.5) * img.height / self.H))
                for x in range(self.W):
                    i = y * self.W + x
                    if self.solid[i] and a[min(img.width - 1, int((x + 0.5) * img.width / self.W)), img.height - 1 - sy] > 127: self.hard[i] = 1
        for row, r in enumerate(rows):
            y0 = (self.R - 1 - row) * CS
            for col, ch in enumerate(r):
                x0 = col * CS
                if (art or coll) and ch == 'X':
                    for yy in range(y0, y0 + CS):
                        for xx in range(x0, x0 + CS):
                            if self.is_solid(xx, yy) and 0 <= xx < self.W and 0 <= yy < self.H: self.hard[yy * self.W + xx] = 1
                    continue
                if (art or coll) and ch not in 'HhE': continue
                if ch == '#': self.fill(x0, y0, x0 + CS, y0 + CS)
                elif ch == 'X': self.fill(x0, y0, x0 + CS, y0 + CS, True)
                elif ch in '/\\':
                    for i in range(CS):
                        h = i + 1 if ch == '/' else CS - i
                        self.fill(x0 + i, y0, x0 + i + 1, y0 + h)
                elif ch in 'Hh': self.hatches.append((x0 + 4, y0 - 2, -1 if ch == 'h' else 1))
                elif ch == 'E': self.exits.append((x0 + 4 - 4, y0, 9, 10))
        if self.meta.get("exit", "") == "mother" and self.hatches:
            hx = self.hatches[0][0]; wide = []
            for (ex, ey, w, h) in self.exits:
                cx = ex + 4; side = 1 if hx <= cx else -1
                a, b = cx - side * 6, cx + side * 26
                wide.append((min(a, b), ey, abs(b - a), 12))
            self.exits = wide
        self.exits += goals
        self.cats = int(self.meta.get("cats", 10)); self.need = int(self.meta.get("need", 5))
        self.rate = float(self.meta.get("rate", 1.5))
    def fill(self, x0, y0, x1, y1, hard=False):
        for y in range(max(0, y0), min(self.H, y1)):
            for x in range(max(0, x0), min(self.W, x1)):
                self.solid[y * self.W + x] = 7
                if hard: self.hard[y * self.W + x] = 1
    def is_solid(self, x, y):
        if x < self.ax0 or x >= self.ax1: return True
        if y < self.ay0 or y >= self.ay1: return False
        return (self.solid[y * self.W + x] & self.plane) != 0
    def is_support(self, x, y):
        if not self.lanejump: return self.is_solid(x, y)
        if x < self.ax0 or x >= self.ax1: return True
        if y < self.ay0 or y >= self.ay1: return False
        return self.solid[y * self.W + x] != 0
    def built_my_lane(self, x, y):
        if x < 0 or x >= self.W or y < 0 or y >= self.H: return False
        i = y * self.W + x
        return self.built[i] == 1 and (self.builtlane[i] & self.plane) != 0
    def mask_at(self, x, y):
        if x < 0 or x >= self.W or y < 0 or y >= self.H: return 0
        return self.solid[y * self.W + x]
    def is_hard(self, x, y):
        if x < 0 or x >= self.W or y < 0 or y >= self.H: return False
        i = y * self.W + x
        return self.hard[i] == 1 and (self.solid[i] & self.plane) != 0
    def has_hard(self, x0, y0, x1, y1):
        return any(self.is_hard(x, y) for y in range(y0, y1) for x in range(x0, x1))
    def carve_rect(self, x0, y0, x1, y1):
        for y in range(max(0, y0), min(self.H, y1)):
            for x in range(max(0, x0), min(self.W, x1)):
                i = y * self.W + x
                if (self.solid[i] & self.plane) and not self.hard[i]: self.solid[i] = 0 if self.built[i] else (self.solid[i] & ~self.plane & 7); self.built[i] = 0
    def carve_circle(self, cx, cy, r):
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r: self.carve_rect(cx + dx, cy + dy, cx + dx + 1, cy + dy + 1)
    def add_rect(self, x0, y0, x1, y1):
        for y in range(max(0, y0), min(self.H, y1)):
            for x in range(max(0, x0), min(self.W, x1)):
                i = y * self.W + x
                if not (self.solid[i] & self.plane): self.solid[i] = 7; self.built[i] = 1; self.builtlane[i] = self.plane
    def count_solid(self, x0, y0, x1, y1):
        n = 0
        for y in range(max(0, y0), min(self.H, y1)):
            for x in range(max(0, x0), min(self.W, x1)):
                if self.solid[y * self.W + x]: n += 1
        return n
    def is_built(self, x, y):
        if x < 0 or x >= self.W or y < 0 or y >= self.H: return False
        i = y * self.W + x
        return self.built[i] == 1 and (self.solid[i] & self.plane) != 0
    def at_exit(self, x, y):
        return any(ex <= x < ex + w and ey <= y < ey + h for ex, ey, w, h in self.exits)

class Cat:
    def __init__(s, g, lv, x, y):
        s.g, s.lv, s.x, s.y, s.dir = g, lv, x, y, 1
        s.plane = lv.startlane or (1 if lv.front_y > 0 else 7)
        s.state = "fall"; s.fd = 0; s.t = 0; s.chute = s.hasclimb = False
        s.fuse = -1; s.balls = 0; s.steps = 0
        s.rail = -1; s.rsign = 1; s.theta = 0.0; s.grace = 0; s.cstart = 0
    def step_limit(s): return 9 + (4 if s.lv.is_built(s.x, s.y - 1) or s.lv.is_built(s.x, s.y - 2) else 0)
    def adopt(s, sx, sy):
        if not s.lv.lanejump: return
        m = s.lv.mask_at(sx, sy)
        if m == 0 or (m & s.plane): return
        s.plane = 4 if m & 4 else 2 if m & 2 else 1
    def active(s): return s.state in ("walk", "fall", "dig", "block", "zoom", "climb", "build", "scratch", "rail")
    def give(s, trait):
        if s.state == "rail" and trait not in ("Fluffchute", "CurtainClimber", "CableChewer"):
            s.state = "walk"; s.rail = -1; s.grace = 90          # steps off the rim to use the trait
            up = 0
            while up < 3 and s.lv.is_solid(s.x, s.y + up): up += 1
            s.y += up
            for d in range(4):
                if s.lv.is_support(s.x, s.y - 1 - d): s.y -= d; s.adopt(s.x, s.y - 1); break
        st = s.state
        if trait == "LitterLord" and st == "walk": s.state = "dig"; s.t = 0; return True
        if trait == "BiscuitBaker" and st == "walk": s.state = "block"; return True
        if trait == "Zoomies" and st == "walk": s.state = "zoom"; s.t = 0; return True
        if trait == "Scratcher" and st == "walk": s.state = "scratch"; s.t = 0; return True
        if trait == "HairballBridge" and st == "build": s.balls += 12; s.pause = 0; s.t = 0; return True
        if trait == "HairballBridge" and st == "walk": s.state = "build"; s.balls = 12; s.t = 0; s.steps = 0; return True
        if trait == "Fluffchute" and not s.chute and st in ("walk", "fall", "dig", "rail"): s.chute = True; return True
        if trait == "CurtainClimber" and not s.hasclimb and st != "block": s.hasclimb = True; return True
        if trait == "CableChewer" and s.fuse < 0: s.fuse = 150; return True
        return False
    def tick(s):
        s.lv.plane = s.plane
        try: s.tick_inner()
        finally: s.lv.plane = 7
    def tick_inner(s):
        lv = s.lv
        if s.active() and s.fuse > 0:
            s.fuse -= 1
            if s.fuse == 0:
                lv.carve_circle(s.x, s.y + 1, 12); s.state = "zapped"
                for o in lv.objects:
                    if o[0] == "trap" and o[6] and ((o[1] + o[3]) / 2 - s.x) ** 2 + ((o[2] + o[4]) / 2 - s.y - 1) ** 2 <= 22 * 22: o[6] = False
                return
        ({"rail": s.rail_tick}.get(s.state) or getattr(s, s.state))() if s.state in ("walk", "fall", "dig", "block", "zoom", "climb", "build", "scratch", "rail") else None
        if s.active():
            if lv.at_exit(s.x, s.y): s.state = "saved"
            elif s.y < lv.ay0 - 20: s.state = "lost"
    def blocked(s, nx):
        for c in s.g.cats:
            if c is s or c.state != "block": continue
            if abs(c.y - s.y) > 8: continue
            infront = c.x > s.x if nx > s.x else c.x < s.x
            if infront and abs(c.x - nx) <= 4: return True
        return False
    # ---- curved paths ----
    def attach(s, i, t):
        s.rail, s.theta = i, t
        s.rsign = -s.dir * (1 if math.sin(t) >= 0 else -1)
        s.state = "rail"; s.fd = 0
        s.plane = s.path_lane(i, math.sin(t) > 0)
    def path_lane(s, i, back):
        p = s.lv.paths[i]; lane = p["bl"] if back else p["fl"]
        return lane or (1 if back else 2)
    def try_land_rail(s):
        if s.grace > 0: return False
        for i, p in enumerate(s.lv.paths):
            u = (s.x - p["cx"]) / p["a"]
            if u < -1 or u > 1: continue
            h = p["b"] * math.sqrt(1 - u * u)
            for sg in (1, -1):
                yv = p["cy"] + sg * h
                if not (s.y >= yv and s.y - 1 < yv): continue
                t = math.atan2(sg * h / p["b"], u)
                if not p["closed"] and (t < p["d0"] or t > p["d1"]): continue
                s.y = int(math.floor(yv + 0.5))
                if s.fd > 60 and not s.chute: s.state = "splat"; return True
                s.attach(i, t); return True
        return False
    def try_attach(s):
        if s.grace > 0: s.grace -= 1; return
        for i, p in enumerate(s.lv.paths):
            t = math.atan2((s.y - p["cy"]) / p["b"], (s.x - p["cx"]) / p["a"])
            if not p["closed"] and (t < p["d0"] or t > p["d1"]): continue
            qx, qy = p["cx"] + p["a"] * math.cos(t), p["cy"] + p["b"] * math.sin(t)
            if (qx - s.x) ** 2 + (qy - s.y) ** 2 > 2.25: continue
            s.attach(i, t); return
    def rail_tick(s):
        if s.rail < 0 or s.rail >= len(s.lv.paths): s.state = "walk"; return
        p = s.lv.paths[s.rail]
        for h in range(4):                      # stairs built right next to the path: step off and climb them
            if s.lv.built_my_lane(s.x + s.dir, s.y + h):
                s.state = "walk"; s.rail = -1; s.grace = 60
                up = 0
                while up < 3 and s.lv.is_solid(s.x, s.y + up): up += 1
                s.y += up
                for d in range(4):
                    if s.lv.is_support(s.x, s.y - 1 - d): s.y -= d; s.adopt(s.x, s.y - 1); break
                return
        sp = math.sqrt(p["a"] ** 2 * math.sin(s.theta) ** 2 + p["b"] ** 2 * math.cos(s.theta) ** 2)
        nt = s.theta + s.rsign / max(0.5, sp)
        if not p["closed"] and (nt < p["d0"] or nt > p["d1"]):
            if p.get("slide"): s.state = "fall"; s.fd = 0; s.rail = -1; s.grace = 30; return
            s.rsign = -s.rsign; return
        nx, ny = int(math.floor(p["cx"] + p["a"] * math.cos(nt) + 0.5)), int(math.floor(p["cy"] + p["b"] * math.sin(nt) + 0.5))
        blocked = any(c is not s and c.state == "block" and abs(c.x - nx) <= 4 and abs(c.y - ny) <= 8 for c in s.g.cats)
        for (x0, y0, x1, y1, nm, side) in s.lv.railblocks:
            if nm and nm != p["name"]: continue
            if side == "back" and math.sin(nt) <= 0: continue
            if side == "front" and math.sin(nt) > 0: continue
            ax0, ax1, ay0, ay1 = max(nx - 4, x0), min(nx + 5, x1), max(ny + 2, y0), min(ny + 10, y1)
            if ax1 > ax0 and ay1 > ay0 and s.lv.count_solid(ax0, ay0, ax1, ay1) >= 3: blocked = True
        if blocked: s.rsign = -s.rsign; return
        if nx != s.x: s.dir = 1 if nx > s.x else -1
        s.theta = nt % (2 * math.pi) if p["closed"] else nt
        s.plane = s.path_lane(s.rail, math.sin(s.theta) > 0)
        s.x, s.y = nx, ny
    def walk(s):
        s.walk_step()
        if s.state == "walk": s.try_attach()
    def walk_step(s):
        lv = s.lv; nx = s.x + s.dir
        if s.blocked(nx): s.dir = -s.dir; return
        if lv.is_solid(nx, s.y):
            h = 1
            lim = s.step_limit()
            while h <= lim and lv.is_solid(nx, s.y + h): h += 1
            if h > lim:
                if s.hasclimb and lv.ax0 <= nx < lv.ax1 and not lv.is_hard(nx, s.y + 1): s.state = "climb"; s.t = 0; s.cstart = s.y
                else: s.dir = -s.dir
                return
            s.x = nx; s.y += h; return
        s.x = nx
        for d in range(4):
            if lv.is_solid(s.x, s.y - 1 - d): s.y -= d; return
        for d in range(4):
            if lv.is_support(s.x, s.y - 1 - d): s.y -= d; s.adopt(s.x, s.y - 1); return
        s.state = "fall"; s.fd = 0
    def floating(s): return s.state == "fall" and s.chute and s.fd >= 12
    def fall(s):
        sp = 1 if s.floating() else 3
        for _ in range(sp):
            if s.lv.is_support(s.x, s.y - 1):
                if s.fd > 60 and not s.chute: s.state = "splat"
                else:
                    s.state = "walk"
                    s.adopt(s.x, s.y - 1)
                    if s.plane == 1 and s.y <= s.lv.front_y: s.plane = 2
                return
            if s.try_land_rail(): return
            s.y -= 1; s.fd += 1
    def dig(s):
        s.t += 1
        if s.t < 4: return
        s.t = 0; lv = s.lv
        if not any(lv.is_solid(s.x + dx, s.y - 1) for dx in range(-4, 5)): s.state = "fall"; s.fd = 0; return
        if lv.has_hard(s.x - 4, s.y - 1, s.x + 5, s.y): s.state = "walk"; return
        lv.carve_rect(s.x - 4, s.y - 1, s.x + 5, s.y); s.y -= 1
    def block(s):
        if not s.lv.is_support(s.x, s.y - 1): s.state = "fall"; s.fd = 0
    def solid_ahead(s, dist, lo, hi):
        for i in range(1, dist + 1):
            cx = s.x + s.dir * i
            if cx < s.lv.ax0 or cx >= s.lv.ax1: return False
            if any(s.lv.is_solid(cx, s.y + h) for h in range(lo, hi)): return True
        return False
    def zoom(s):
        s.t += 1
        if s.t < 2: return
        s.t = 0; lv = s.lv; nx = s.x + s.dir
        if nx < lv.ax0 or nx >= lv.ax1 or s.blocked(nx): s.dir = -s.dir; s.state = "walk"; return
        if not s.solid_ahead(12, 0, 11): s.state = "walk"; return
        a, b = s.x + s.dir, s.x + s.dir * 5
        if lv.has_hard(min(a, b), s.y + 2, max(a, b) + 1, s.y + 6): s.state = "walk"; return
        lv.carve_rect(min(a, b), s.y, max(a, b) + 1, s.y + 11); s.x = nx
        rise = 0
        while rise <= 9 and lv.is_solid(s.x, s.y + rise): rise += 1
        if rise <= 9: s.y += rise
        for d in range(4):
            if lv.is_solid(s.x, s.y - 1 - d): s.y -= d; return
        s.state = "fall"; s.fd = 0
    def climb(s):
        s.t += 1
        if s.t < 2: return
        s.t = 0; lv = s.lv
        if lv.is_solid(s.x, s.y + 10) or s.y + 10 >= lv.H: s.dir = -s.dir; s.state = "fall"; s.fd = 0; return
        if s.y - s.cstart >= 100 or lv.is_hard(s.x + s.dir, s.y + 1): s.dir = -s.dir; s.state = "fall"; s.fd = 0; return
        s.y += 1
        if not lv.is_solid(s.x + s.dir, s.y): s.x += s.dir; s.state = "walk"
    def scratch(s):
        s.t += 1
        if s.t < 4: return
        s.t = 0; lv = s.lv; nx = s.x + s.dir
        if nx < lv.ax0 or nx >= lv.ax1: s.dir = -s.dir; s.state = "walk"; return
        if not s.solid_ahead(6, -1, 10): s.state = "walk"; return
        a, b = s.x + s.dir, s.x + s.dir * 4
        if lv.has_hard(min(a, b), s.y - 1, max(a, b) + 1, s.y + 3): s.state = "walk"; return
        lv.carve_rect(min(a, b), s.y - 1, max(a, b) + 1, s.y + 10); s.x = nx; s.y -= 1
        if not lv.is_solid(s.x, s.y - 1): s.state = "fall"; s.fd = 0
    def build(s):
        lv = s.lv
        if not lv.is_support(s.x, s.y - 1): s.state = "fall"; s.fd = 0; return
        if s.steps > 0:
            nx = s.x + s.dir
            if nx < lv.ax0 or nx >= lv.ax1: s.dir = -s.dir; s.state = "walk"; return
            h = 0
            lim = s.step_limit()
            while h <= lim and lv.is_solid(nx, s.y + h): h += 1
            if h > lim or lv.is_solid(nx, s.y + h + 6): s.dir = -s.dir; s.state = "walk"; return
            s.x = nx; s.y += h; s.steps -= 1
            return
        if s.balls <= 0:                      # wait a second for the player to click again
            s.pause = getattr(s, "pause", 0) + 1
            if s.pause >= 30: s.state = "walk"
            return
        s.t += 1
        if s.t < 30: return
        s.t = 0
        a, b = s.x + s.dir, s.x + s.dir * 6
        lv.add_rect(min(a, b), s.y, max(a, b) + 1, s.y + 2); s.balls -= 1; s.steps = 4

class Game:
    """plan: list of (condition_fn(game, cat) -> bool, trait). Each step is applied once, to the first cat that matches."""
    def __init__(s, n, plan=(), max_secs=300):
        s.lv = Level(n); s.cats = []; s.all = []; s.plan = list(plan); s.max_ticks = max_secs * 30
    def check_objects(s):
        for c in s.all:
            if not c.active(): continue
            for o in s.lv.objects:
                typ, x0, y0, x1, y1, extra, on = o
                feet = x0 <= c.x < x1 and y0 <= c.y < y1
                body = x0 - 2 <= c.x < x1 + 2 and c.y < y1 and c.y + 11 > y0
                if typ == "water" and feet: c.state = "soaked"; break
                if typ == "fire" and on and feet: c.state = "burnt"; break
                if typ == "trap" and on and feet: c.state = "snapped"; break
                if typ == "switch" and on and body:
                    o[6] = False
                    for f in s.lv.objects:
                        if f[0] == "fire" and f[5][:1] == extra[:1]: f[6] = False
                if typ == "prop" and on and body: o[6] = False
                if typ == "secret" and on and body and not any(p[0] == "prop" and p[6] and p[1] < x1 and x0 < p[3] and p[2] < y1 and y0 < p[4] for p in s.lv.objects):
                    o[6] = False; s.secrets = getattr(s, "secrets", 0) + 1
    def run(s):
        lv = s.lv; released = 0; nxt = 30; t = 0
        while t < s.max_ticks:
            t += 1
            if released < lv.cats:
                nxt -= 1
                if nxt <= 0:
                    hx, hy, hd = lv.hatches[released % len(lv.hatches)]
                    c = Cat(s, lv, hx, hy); c.dir = hd; s.cats.append(c); s.all.append(c); released += 1
                    nxt = max(1, round(lv.rate * 30))
            if s.plan:
                cond, trait = s.plan[0]
                for c in s.cats:
                    if cond(s, c) and c.give(trait): s.plan.pop(0); break
            for c in list(s.cats): c.tick()
            s.check_objects()
            s.cats = [c for c in s.all if c.active()]
            if released == lv.cats and all(c.state == "block" for c in s.cats): break
        res = {}
        for c in s.all: res[c.state] = res.get(c.state, 0) + 1
        return res, t / 30

def check(n, desc, plan=(), expect_win=True):
    g = Game(n, plan); res, secs = g.run()
    saved = res.get("saved", 0)
    ok = (saved >= g.lv.need) == expect_win
    label = n if isinstance(n, str) else f"L{n:02d}"
    print(f"{'OK  ' if ok else 'FAIL'} {label:<7} {desc:<45} saved {saved}/{g.lv.cats} need {g.lv.need}  {res}  {secs:.0f}s")
    return ok

W = lambda c: c.state == "walk"
def walking_near(x0, x1, y=None, d=None):
    return lambda g, c: c.state == "walk" and x0 <= c.x <= x1 and (y is None or c.y == y) and (d is None or c.dir == d)

if __name__ == "__main__":
    allok = True
    for n in range(1, 11):
        allok &= check(n, "no traits used (should FAIL)", expect_win=False)
    def l1(x0, extra_clicks=2):
        plan = [(lambda g, c: c.state == "walk" and c.y == 120 and 282 <= c.x <= 296, "LitterLord"),
                (lambda g, c: c.state == "walk" and c.y == 98 and 318 <= c.x <= 332, "LitterLord"),
                (lambda g, c: c.state == "walk" and c.y == 75 and 350 <= c.x <= 364, "LitterLord"),
                (lambda g, c, x0=x0: c.state == "walk" and c.y == 53 and c.dir == 1 and c.x == x0, "HairballBridge")]
        plan += [(lambda g, c: c.state == "build" and c.balls <= 2, "HairballBridge")] * extra_clicks
        return plan
    for x0 in (388, 376, 368, 360, 340):
        allok &= check(1, f"Dig 3 steps, bridge from x={x0} (+2 clicks)", l1(x0, 2))
    allok &= check(1, "bridge from x=404, no extra clicks (should FAIL: too short)", l1(404, 0), expect_win=False)
    allok &= check(1, "bridge from x=404 too late: bumps the cushion (should FAIL)", l1(404, 2), expect_win=False)
    # the stool leg must not leave a gap in the bottom step: a kitten walking the whole step never falls
    g = Game(1, []); c = Cat(g, g.lv, 320, 75); c.plane = 1; c.state = "walk"; g.all = [c]; g.cats = [c]
    fell = False
    for t in range(30 * 40):
        c.tick()
        if c.state == "fall": fell = True
    print(("OK   " if not fell else "FAIL ") + "L01     bottom step has no gap under the stool leg"); allok &= not fell
    # ---- Level 2 sandbox: rim loops, blockers and the ways off the rim ----
    import math as _m
    g2 = Game(2, []); g2.max_ticks = 30 * 120; r2 = g2.run()[0]
    print(("OK   " if r2.get("rail", 0) == 10 else "FAIL ") + f"L02     no traits: every kitten ends up looping the rim {r2}"); allok &= r2.get("rail", 0) == 10
    plan = [(lambda g, c: c.state == "rail" and _m.sin(c.theta) > 0.2 and 471 <= c.x <= 477 and c.dir == -1, "Zoomies")]
    def reach(plan_, label):
        g_ = Game(2, plan_); g_.max_ticks = 30 * 200; vis = set(); o = Cat.tick
        def t_(c_):
            o(c_)
            if c_.state == "rail" and _m.sin(c_.theta) > 0.3: vis.add(c_.x)
        Cat.tick = t_; g_.run(); Cat.tick = o
        return any(380 <= x <= 430 for x in vis)
    base = reach([], "none"); after = reach(plan, "zoomies")
    ok = (not base) and after
    print(("OK   " if ok else "FAIL ") + f"L02     back edge between taps and vase: unreachable without Zoomies ({not base}), reachable after Zoomies through the vase ({after})"); allok &= ok
    # stairs from the rim: the OTHER kittens must follow them (they used to loop the rim for ever)
    plan = [(lambda g, c: c.state == "rail" and _m.sin(c.theta) < -0.5 and c.dir == 1 and 455 <= c.x <= 470, "HairballBridge"),
            (lambda g, c: c.state == "build" and c.balls <= 2, "HairballBridge")]
    g2 = Game(2, plan); g2.max_ticks = 30 * 200; o = Cat.tick; climbers = set()
    def t_(c_):
        o(c_)
        if c_.state in ("walk", "fall") and c_.x > 500 and c_.y > 215: climbers.add(id(c_))
    Cat.tick = t_; r2 = g2.run()[0]; Cat.tick = o
    ok = len(climbers) >= 3; print(("OK   " if ok else "FAIL ") + f"L02     other kittens follow the fur stairs off the rim: {len(climbers)} kittens climbed them {r2}"); allok &= ok
    # ...and a Fluffchute makes the drop off the bridge survivable (kittens that run on before it is finished still fall
    # through the gap - that is a puzzle for the player: wait for the bridge or use Mum/blockers)
    plan2 = plan + [(lambda g, c: c.state == "fall" and c.x > 485 and c.fd >= 20 and not c.chute, "Fluffchute")] * 12
    g2 = Game(2, plan2); g2.max_ticks = 30 * 200; r2 = g2.run()[0]
    ok = r2.get("saved", 0) >= 1 and all(c.state != "splat" for c in g2.all if c.chute)
    print(("OK   " if ok else "FAIL ") + f"L02     Fluffchute kittens never splat off the bridge, at least one reaches the counter {r2}"); allok &= ok
    # ---- Level 2: every surface can be reached and behaves (one kitten dropped from above each) ----
    def drop(x, y, dirn=1, secs=40, pln=()):
        g_ = Game(2, list(pln)); g_.lv.hatches = [(x, y, dirn)]; g_.lv.cats = 1; g_.max_ticks = 30 * secs
        tr = []; o = Cat.tick
        def t_(c_): o(c_); tr.append((c_.state, c_.x, c_.y, c_.rail if c_.state == "rail" else -1))
        Cat.tick = t_; g_.run(); Cat.tick = o
        return tr
    def pid(name): return [i for i, p in enumerate(Level(2).paths) if p["name"] == name][0]
    for nm, (px, py) in {"lid": (81, 175), "sink": (600, 235)}.items():
        tr = drop(px, py, 1, 20); ok = tr[-1][0] == "rail" and tr[-1][3] == pid(nm)
        print(("OK   " if ok else "FAIL ") + f"L02     kitten dropped above the {nm} lands on its path and runs round it {tr[-1]}"); allok &= ok
    for nm, (px, py) in {"lampL": (312, 440), "lampR": (704, 440)}.items():
        tr = drop(px, py, 1, 30); on = any(t[0] == "rail" and t[3] == pid(nm) for t in tr)
        ok = on and tr[-1][0] in ("walk", "fall", "splat") and tr[-1][3] == -1
        print(("OK   " if ok else "FAIL ") + f"L02     {nm}: kitten lands on top of the light, then slides off it {tr[-1]} (on the cap: {on})"); allok &= ok
    # dais: walking the lower tier from its left end carries a kitten along the whole curved lip, onto the stool and round its top
    tr = drop(150, 175, 1, 60); xs = [t[1] for t in tr]; stool = any(t[3] == pid("stool") for t in tr)
    ok = max(xs) >= 520 and stool
    print(("OK   " if ok else "FAIL ") + f"L02     dais lower tier curve: walks to x={max(xs)}, steps onto the stool and runs its top ({stool})"); allok &= ok
    # shelves / ledges: kitten dropped just above each ends up standing ON it
    ledges = {"left shelf 1": (60, 400, 386), "left shelf 2": (60, 320, 306), "gold shelf A": (520, 350, 337), "gold shelf B": (520, 305, 292), "window bar 1": (420, 390, 378),
              "window bar 2": (420, 340, 328), "window bar 3": (420, 290, 277), "cat frame top": (170, 355, 342), "right sconce top": (738, 380, 367), "niche shelf 1": (800, 265, 252)}
    for nm, (px, py, top) in ledges.items():
        tr = drop(px, py, 1, 3); land = [t for t in tr if t[0] == "walk"]
        ok = bool(land) and abs(land[0][2] - top) <= 3
        print(("OK   " if ok else "FAIL ") + f"L02     ledge '{nm}': dropped kitten lands on it (feet y {land[0][2] if land else None}, wanted {top})"); allok &= ok
    allok &= check(3, "Baker in front of the cliff", [(walking_near(470, 480, 184), "BiscuitBaker")])
    allok &= check(4, "Zoomies next to the wall", [(walking_near(310, 318, 112, 1), "Zoomies")])
    allok &= check(5, "Climber for every cat", [(lambda g, c: not c.hasclimb and c.state in ("walk", "fall"), "CurtainClimber")] * 10)
    allok &= check(8, "Baker, bridge at the gap, zap baker", [(walking_near(232, 236, 144, 1), "HairballBridge"),
                                                    (walking_near(140, 160, 144), "BiscuitBaker"),
                                                    (lambda g, c: c.state == "block" and any(o.x > 290 and o.state in ("walk", "saved") for o in g.all), "CableChewer")])
    allok &= check(7, "Scratcher before the lip", [(lambda g, c: c.state == "walk" and c.dir == 1 and 250 <= c.x <= 260 and c.y == 200, "Scratcher")])
    allok &= check(6, "Cable Chewer on upper floor", [(walking_near(150, 250, 184), "CableChewer")])
    allok &= check(9, "Climber + Fluffchute every cat", ([(lambda g, c: not c.hasclimb and c.state in ("walk", "fall"), "CurtainClimber"),
                                                        (lambda g, c: c.hasclimb and not c.chute and c.state in ("walk", "fall", "climb"), "Fluffchute")] * 10))
    allok &= check(10, "Baker, bridge, zoom wall, scratch cliff, free baker", [
        (walking_near(232, 236, 232, 1), "HairballBridge"),
        (walking_near(130, 170, 232), "BiscuitBaker"),
        (walking_near(428, 436, 232, 1), "Zoomies"),
        (walking_near(476, 484, 232, 1), "Scratcher"),
        (lambda g, c: c.state == "block" and any(o.state == "saved" for o in g.all), "CableChewer")])
    allok &= check("kitchen", "no traits used (should FAIL: mouse trap)", expect_win=False)
    allok &= check("kitchen", "Floor route: bridge over the mouse trap", [(walking_near(796, 806, 40, 1), "HairballBridge")])
    allok &= check("kitchen", "Floor route: zap the mouse trap", [(walking_near(662, 668, 40, 1), "CableChewer")])
    print("ALL GOOD" if allok else "SOME CHECKS FAILED")
