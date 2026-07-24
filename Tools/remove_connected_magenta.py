from collections import deque
from pathlib import Path
import sys

from PIL import Image


def is_magenta(r: int, g: int, b: int) -> bool:
    return (
        r >= 180
        and b >= 145
        and g <= 105
        and r - g >= 105
        and b - g >= 90
    )


source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])

source = Image.open(source_path).convert("RGBA")
pixels = source.load()
width, height = source.size
background = bytearray(width * height)
queue: deque[tuple[int, int]] = deque()


def enqueue(x: int, y: int) -> None:
    index = y * width + x
    if background[index]:
        return
    r, g, b, _ = pixels[x, y]
    if not is_magenta(r, g, b):
        return
    background[index] = 1
    queue.append((x, y))


for x in range(width):
    enqueue(x, 0)
    enqueue(x, height - 1)

for y in range(height):
    enqueue(0, y)
    enqueue(width - 1, y)

while queue:
    x, y = queue.popleft()
    if x > 0:
        enqueue(x - 1, y)
    if x + 1 < width:
        enqueue(x + 1, y)
    if y > 0:
        enqueue(x, y - 1)
    if y + 1 < height:
        enqueue(x, y + 1)

result = source.copy()
result_pixels = result.load()
removed = 0

for y in range(height):
    for x in range(width):
        if background[y * width + x]:
            r, g, b, _ = result_pixels[x, y]
            result_pixels[x, y] = (r, g, b, 0)
            removed += 1

output_path.parent.mkdir(parents=True, exist_ok=True)
result.save(output_path)
print(f"Removed connected magenta pixels: {removed}/{width * height}")
