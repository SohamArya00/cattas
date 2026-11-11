"""Generates the starter level files (Assets/Resources/Levels/levelNN.txt).
Coordinates here are in 8px blocks, measured from the BOTTOM-LEFT (cy=0 is the bottom row).
After generating, the .txt files are the real source - feel free to edit them by hand."""
import os

ROWS = 45

class Map:
    def __init__(self, cols, rows=ROWS):
        self.cols, self.rows = cols, rows
        self.g = [['.'] * cols for _ in range(rows)]
    def put(self, cx, cy, ch):
        if 0 <= cx < self.cols and 0 <= cy < self.rows:
            self.g[self.rows - 1 - cy][cx] = ch
    def rect(self, cx0, cy0, cx1, cy1, ch='#'):   # cx1/cy1 exclusive
        for cy in range(cy0, cy1):
            for cx in range(cx0, cx1):
                self.put(cx, cy, ch)
    def text(self):
        return "\n".join("".join(r) for r in self.g)

def write(n, name, hint, cats, need, traits, m, time=300, rate=1.5):
    t = ", ".join(f"{k}={v}" for k, v in traits.items())
    body = f"""// Cattas level {n}
// Map key:  .=air  #=dirt  X=stone (can't dig)  /=ramp up-right  \\=ramp up-left  H=hatch  E=exit
name: {name}
hint: {hint}
cats: {cats}
need: {need}
rate: {rate}
time: {time}
traits: {t}
map:
{m.text()}
"""
    path = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Levels", f"level{n:02d}.txt")
    with open(path, "w", newline="\n") as f:
        f.write(body)

# 1 - Litter Lord: dig down to the exit
m = Map(80)
m.rect(4, 24, 40, 28)          # upper floor (top at 224px)
m.rect(2, 24, 4, 36); m.rect(40, 24, 42, 36)   # walls keep cats on it
m.rect(0, 17, 72, 21)          # lower floor (top at 168px)
m.rect(0, 21, 2, 30); m.rect(72, 17, 74, 30)   # walls on the lower floor
m.put(10, 30, 'H'); m.put(62, 21, 'E')
write(1, "Dig In", "Pick Litter Lord, then click a cat to dig down to the exit.", 10, 5, {"LitterLord": 3}, m)

# 2 - Fluffchute: big drop
m = Map(80)
m.rect(5, 33, 25, 36)          # high starting ledge (top at 288px)
m.rect(3, 33, 5, 42)
m.rect(0, 6, 78, 10)           # ground far below (top at 80px)
m.rect(0, 10, 2, 20); m.rect(76, 10, 78, 24)
m.put(10, 38, 'H'); m.put(62, 10, 'E')
write(2, "Soft Landing", "That drop is too high! Give cats a Fluffchute before (or while) they fall.", 10, 5, {"Fluffchute": 10}, m)

# 3 - Biscuit Baker: turn them away from the cliff
m = Map(80)
m.rect(5, 20, 70, 23)          # long floor ending in a cliff on the right (top 184px)
m.rect(3, 20, 5, 30)           # wall behind the exit
m.put(50, 28, 'H'); m.put(10, 23, 'E')
write(3, "Knead to Know", "Cats walk towards the cliff! Make a Biscuit Baker to turn them round.", 10, 8, {"BiscuitBaker": 2}, m)

# 4 - Zoomies: smash through the wall
m = Map(80)
m.rect(0, 10, 78, 14)          # floor (top 112px)
m.rect(0, 14, 2, 30); m.rect(78, 10, 80, 30)
m.rect(40, 14, 43, 32)         # thick tall wall in the way
m.put(10, 20, 'H'); m.put(65, 14, 'E')
write(4, "Zoom Zoom", "A wall is in the way. Give a cat the Zoomies when it's right next to it.", 10, 8, {"Zoomies": 2}, m)

# 5 - Curtain Climber: exit up on a plateau
m = Map(80)
m.rect(0, 6, 80, 10)           # ground (top 80px)
m.rect(0, 10, 2, 26)
m.rect(45, 10, 80, 18)         # plateau (top 144px) - a 64px wall to climb
m.put(10, 16, 'H'); m.put(68, 18, 'E')
write(5, "Up the Curtains", "The exit is up high. Curtain Climbers can climb any wall.", 10, 6, {"CurtainClimber": 10}, m)

# 8 - Hairball Bridge: cross the gap (with a Biscuit Baker to hold the queue)
m = Map(80)
m.rect(2, 15, 30, 18)          # left platform (top 144px)
m.rect(0, 15, 2, 26)
m.rect(34, 15, 78, 18)         # right platform, past a 32px gap
m.rect(78, 15, 80, 26)
m.put(8, 22, 'H'); m.put(65, 18, 'E')
write(8, "Hairy Situation", "Hold the others back with a Biscuit Baker, hack up a Hairball Bridge at the edge, then ZAP the baker free.", 10, 7,
      {"HairballBridge": 2, "BiscuitBaker": 1, "CableChewer": 1}, m)

# 7 - Scratcher: dig diagonally down a cliff
m = Map(80)
m.rect(0, 10, 80, 15, 'X')     # stone base (top 120px)
m.rect(2, 15, 40, 25)          # big dirt plateau (top 200px), 80px cliff on its right
m.rect(0, 15, 2, 32)
m.rect(38, 25, 40, 27)         # little lip at the cliff edge turns cats round
m.rect(78, 15, 80, 30)
m.put(8, 30, 'H'); m.put(65, 15, 'E')
write(7, "Diagonal Thinking", "The cliff is too high to jump. A Scratcher can claw a slope down through the dirt.", 10, 7, {"Scratcher": 3}, m)

# 6 - Cable Chewer: blast through the thin floor
m = Map(80)
m.rect(2, 22, 50, 23)          # thin upper floor (top 184px), one block thick
m.rect(0, 22, 2, 34); m.rect(50, 22, 52, 34)
m.rect(0, 12, 78, 16)          # lower floor (top 128px)
m.rect(78, 12, 80, 30)
m.put(10, 28, 'H'); m.put(65, 16, 'E')
write(6, "Bzzzt!", "The floor is thin. A Cable Chewer's ZAP will blast a hole - but that cat won't make it home.", 10, 8, {"CableChewer": 2}, m)

# 9 - Combo: climb the wall, float down the other side
m = Map(80)
m.rect(0, 10, 38, 14)          # left floor (top 112px)
m.rect(0, 14, 2, 30)
m.rect(38, 10, 41, 26)         # tall wall (top 208px)
m.rect(41, 2, 80, 5)           # far-below floor on the right (top 40px)
m.put(10, 20, 'H'); m.put(65, 5, 'E')
write(9, "Athletes", "Climb the wall... and then? Cats can have more than one trait!", 10, 5,
      {"CurtainClimber": 10, "Fluffchute": 10}, m)

# 10 - Graduation: wide scrolling level, everything available
m = Map(120)
m.rect(2, 26, 30, 29)          # start platform (top 232px)
m.rect(0, 26, 2, 40)
m.rect(34, 26, 58, 29)         # middle platform after a 32px gap
m.rect(55, 29, 58, 40)         # tall wall on the middle platform
m.rect(58, 18, 70, 29)         # dirt block ending in an 88px cliff
m.rect(34, 14, 118, 18, 'X')   # stone floor underneath everything on the right (top 144px)
m.rect(118, 14, 120, 30)
m.put(8, 33, 'H'); m.put(110, 18, 'E')
write(10, "Graduation Day", "Everything you've learned! Bridge the gap, get past the wall, then get down the cliff.", 15, 10,
      {"LitterLord": 2, "Fluffchute": 4, "BiscuitBaker": 2, "Scratcher": 2, "CurtainClimber": 2,
       "Zoomies": 2, "CableChewer": 2, "HairballBridge": 2}, m, time=420)
# Kitchen demo - painted level (art from tools/make_kitchen_art.py). Map only marks hatch, exit and stone.
m = Map(80)
m.rect(0, 0, 80, 5, 'X')                       # tiled floor: stone
m.rect(25, 13, 26, 19, 'X'); m.rect(31, 13, 32, 19, 'X'); m.rect(25, 13, 32, 14, 'X')   # steel sink
m.put(9, 31, 'H'); m.put(72, 18, 'E')
body = f"""// Cattas - Kitchen demo (painted level)
// With painted art, the FOREGROUND picture decides what's solid. The map below only marks:
//   H = hatch, E = exit, X = stone (anything solid in an X block can't be dug)
name: Kitchen Chaos
hint: Kittens are climbing in the window! Get them to the cat basket - watch out for the sink and that big fridge...
cats: 15
need: 10
rate: 2
time: 300
background: kitchen_bg
foreground: kitchen_fg
markers: off
traits: LitterLord=2, Fluffchute=5, BiscuitBaker=2, Zoomies=2, CurtainClimber=4, CableChewer=2, Scratcher=2, HairballBridge=2
map:
{m.text()}
"""
open(os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Levels", "kitchen.txt"), "w", newline="\n").write(body)
print("ok")
