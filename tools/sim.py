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
        self.R = len(rows); self.C = max(len(r) for r in rows)
        self.W, self.H = self.C * CS, self.R * CS
        if "size" in self.meta:
            w, h = self.meta["size"].replace(",", " ").split()[:2]; self.W, self.H = int(w), int(h)
        self.objects = []   # [type, x0, y0, x1, y1, extra, on]
        for line in txt:
            st = line.strip()
            if st.startswith("object:"):
                parts = st[7:].split()
                self.objects.append([parts[0], int(parts[1]), int(parts[2]), int(parts[3]), int(parts[4]), parts[5:] , True])
        self.solid = bytearray(self.W * self.H); self.hard = bytearray(self.W * self.H)
        self.hatches, self.exits = [], []
        art = self.meta.get("foreground", "")
        if art:
            from PIL import Image
            img = Image.open(os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Art", art + ".bytes")).convert("RGBA")
            k = img.width // self.W; a = img.split()[3].load()
            for y in range(self.H):
                for x in range(self.W):
                    if a[x * k + k // 2, img.height - 1 - (y * k + k // 2)] > 127: self.solid[y * self.W + x] = 1
        for row, r in enumerate(rows):
            y0 = (self.R - 1 - row) * CS
            for col, ch in enumerate(r):
                x0 = col * CS
                if art and ch == 'X':
                    for yy in range(y0, y0 + CS):
                        for xx in range(x0, x0 + CS):
                            if self.is_solid(xx, yy) and 0 <= xx < self.W and 0 <= yy < self.H: self.hard[yy * self.W + xx] = 1
                    continue
                if art and ch not in 'HE': continue
                if ch == '#': self.fill(x0, y0, x0 + CS, y0 + CS)
                elif ch == 'X': self.fill(x0, y0, x0 + CS, y0 + CS, True)
                elif ch in '/\\':
                    for i in range(CS):
                        h = i + 1 if ch == '/' else CS - i
                        self.fill(x0 + i, y0, x0 + i + 1, y0 + h)
                elif ch == 'H': self.hatches.append((x0 + 4, y0 - 2))
                elif ch == 'E': self.exits.append((x0 + 4 - 4, y0, 9, 10))
        self.cats = int(self.meta.get("cats", 10)); self.need = int(self.meta.get("need", 5))
        self.rate = float(self.meta.get("rate", 1.5))
    def fill(self, x0, y0, x1, y1, hard=False):
        for y in range(max(0, y0), min(self.H, y1)):
            for x in range(max(0, x0), min(self.W, x1)):
                self.solid[y * self.W + x] = 1
                if hard: self.hard[y * self.W + x] = 1
    def is_solid(self, x, y):
        if x < 0 or x >= self.W: return True
        if y < 0 or y >= self.H: return False
        return self.solid[y * self.W + x] == 1
    def is_hard(self, x, y):
        if x < 0 or x >= self.W or y < 0 or y >= self.H: return False
        return self.hard[y * self.W + x] == 1
    def has_hard(self, x0, y0, x1, y1):
        return any(self.is_hard(x, y) for y in range(y0, y1) for x in range(x0, x1))
    def carve_rect(self, x0, y0, x1, y1):
        for y in range(max(0, y0), min(self.H, y1)):
            for x in range(max(0, x0), min(self.W, x1)):
                i = y * self.W + x
                if not self.hard[i]: self.solid[i] = 0
    def carve_circle(self, cx, cy, r):
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r: self.carve_rect(cx + dx, cy + dy, cx + dx + 1, cy + dy + 1)
    def add_rect(self, x0, y0, x1, y1):
        for y in range(max(0, y0), min(self.H, y1)):
            for x in range(max(0, x0), min(self.W, x1)):
                self.solid[y * self.W + x] = 1
    def at_exit(self, x, y):
        return any(ex <= x < ex + w and ey <= y < ey + h for ex, ey, w, h in self.exits)

class Cat:
    def __init__(s, g, lv, x, y):
        s.g, s.lv, s.x, s.y, s.dir = g, lv, x, y, 1
        s.state = "fall"; s.fd = 0; s.t = 0; s.chute = s.hasclimb = False
        s.fuse = -1; s.balls = 0; s.steps = 0
    def active(s): return s.state in ("walk", "fall", "dig", "block", "zoom", "climb", "build", "scratch")
    def give(s, trait):
        st = s.state
        if trait == "LitterLord" and st == "walk": s.state = "dig"; s.t = 0; return True
        if trait == "BiscuitBaker" and st == "walk": s.state = "block"; return True
        if trait == "Zoomies" and st == "walk": s.state = "zoom"; s.t = 0; return True
        if trait == "Scratcher" and st == "walk": s.state = "scratch"; s.t = 0; return True
        if trait == "HairballBridge" and st == "walk": s.state = "build"; s.balls = 12; s.t = 0; s.steps = 0; return True
        if trait == "Fluffchute" and not s.chute and st in ("walk", "fall", "dig"): s.chute = True; return True
        if trait == "CurtainClimber" and not s.hasclimb and st != "block": s.hasclimb = True; return True
        if trait == "CableChewer" and s.fuse < 0: s.fuse = 150; return True
        return False
    def tick(s):
        lv = s.lv
        if s.active() and s.fuse > 0:
            s.fuse -= 1
            if s.fuse == 0:
                lv.carve_circle(s.x, s.y + 1, 12); s.state = "zapped"; return
        getattr(s, s.state)() if s.state in ("walk", "fall", "dig", "block", "zoom", "climb", "build", "scratch") else None
        if s.active():
            if lv.at_exit(s.x, s.y): s.state = "saved"
            elif s.y < -20: s.state = "lost"
    def blocked(s, nx):
        for c in s.g.cats:
            if c is s or c.state != "block": continue
            if abs(c.y - s.y) > 8: continue
            infront = c.x > s.x if nx > s.x else c.x < s.x
            if infront and abs(c.x - nx) <= 4: return True
        return False
    def walk(s):
        lv = s.lv; nx = s.x + s.dir
        if s.blocked(nx): s.dir = -s.dir; return
        if lv.is_solid(nx, s.y):
            h = 1
            while h <= 6 and lv.is_solid(nx, s.y + h): h += 1
            if h > 6:
                if s.hasclimb and 0 <= nx < lv.W: s.state = "climb"; s.t = 0
                else: s.dir = -s.dir
                return
            s.x = nx; s.y += h; return
        s.x = nx
        for d in range(4):
            if lv.is_solid(s.x, s.y - 1 - d): s.y -= d; return
        s.state = "fall"; s.fd = 0
    def floating(s): return s.state == "fall" and s.chute and s.fd >= 12
    def fall(s):
        sp = 1 if s.floating() else 3
        for _ in range(sp):
            if s.lv.is_solid(s.x, s.y - 1):
                if s.fd > 60 and not s.chute: s.state = "splat"
                else: s.state = "walk"
                return
            s.y -= 1; s.fd += 1
    def dig(s):
        s.t += 1
        if s.t < 4: return
        s.t = 0; lv = s.lv
        if not any(lv.is_solid(s.x + dx, s.y - 1) for dx in range(-4, 5)): s.state = "fall"; s.fd = 0; return
        if lv.has_hard(s.x - 4, s.y - 1, s.x + 5, s.y): s.state = "walk"; return
        lv.carve_rect(s.x - 4, s.y - 1, s.x + 5, s.y); s.y -= 1
    def block(s):
        if not s.lv.is_solid(s.x, s.y - 1): s.state = "fall"; s.fd = 0
    def solid_ahead(s, dist, lo, hi):
        for i in range(1, dist + 1):
            cx = s.x + s.dir * i
            if cx < 0 or cx >= s.lv.W: return False
            if any(s.lv.is_solid(cx, s.y + h) for h in range(lo, hi)): return True
        return False
    def zoom(s):
        s.t += 1
        if s.t < 2: return
        s.t = 0; lv = s.lv; nx = s.x + s.dir
        if nx < 0 or nx >= lv.W or s.blocked(nx): s.dir = -s.dir; s.state = "walk"; return
        if not s.solid_ahead(12, 0, 11): s.state = "walk"; return
        a, b = s.x + s.dir, s.x + s.dir * 5
        if lv.has_hard(min(a, b), s.y, max(a, b) + 1, s.y + 11): s.state = "walk"; return
        lv.carve_rect(min(a, b), s.y, max(a, b) + 1, s.y + 11); s.x = nx
        for d in range(4):
            if lv.is_solid(s.x, s.y - 1 - d): s.y -= d; return
        s.state = "fall"; s.fd = 0
    def climb(s):
        s.t += 1
        if s.t < 2: return
        s.t = 0; lv = s.lv
        if lv.is_solid(s.x, s.y + 10) or s.y + 10 >= lv.H: s.dir = -s.dir; s.state = "fall"; s.fd = 0; return
        s.y += 1
        if not lv.is_solid(s.x + s.dir, s.y): s.x += s.dir; s.state = "walk"
    def scratch(s):
        s.t += 1
        if s.t < 4: return
        s.t = 0; lv = s.lv; nx = s.x + s.dir
        if nx < 0 or nx >= lv.W: s.dir = -s.dir; s.state = "walk"; return
        if not s.solid_ahead(6, -1, 10): s.state = "walk"; return
        a, b = s.x + s.dir, s.x + s.dir * 4
        if lv.has_hard(min(a, b), s.y - 1, max(a, b) + 1, s.y + 10): s.state = "walk"; return
        lv.carve_rect(min(a, b), s.y - 1, max(a, b) + 1, s.y + 10); s.x = nx; s.y -= 1
        if not lv.is_solid(s.x, s.y - 1): s.state = "fall"; s.fd = 0
    def build(s):
        lv = s.lv
        if not lv.is_solid(s.x, s.y - 1): s.state = "fall"; s.fd = 0; return
        if s.steps > 0:
            nx = s.x + s.dir
            if nx < 0 or nx >= lv.W or lv.is_solid(nx, s.y + 10) or lv.is_solid(nx, s.y + 3):
                s.dir = -s.dir; s.state = "walk"; return
            if lv.is_solid(nx, s.y):
                h = 1
                while h <= 2 and lv.is_solid(nx, s.y + h): h += 1
                s.y += h
            s.x = nx; s.steps -= 1
            if s.steps == 0 and s.balls == 0: s.state = "walk"
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
                    hx, hy = lv.hatches[released % len(lv.hatches)]
                    c = Cat(s, lv, hx, hy); s.cats.append(c); s.all.append(c); released += 1
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
    allok &= check(1, "Litter Lord on upper floor", [(walking_near(150, 250, 224), "LitterLord")])
    allok &= check(2, "Fluffchute every cat", [(lambda g, c: c.state in ("walk", "fall") and not c.chute, "Fluffchute")] * 10)
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
    allok &= check("kitchen", "no traits used (should FAIL)", expect_win=False)
    allok &= check("kitchen", "Bridge sink, baker, scratch to floor, free baker", [
        (walking_near(336, 343, 200, 1), "HairballBridge"),
        (walking_near(312, 330, 200), "BiscuitBaker"),
        (walking_near(415, 440, 200, 1), "Scratcher"),
        (lambda g, c: c.state == "block" and any(o.state == "saved" for o in g.all), "CableChewer")])
    print("ALL GOOD" if allok else "SOME CHECKS FAILED")
