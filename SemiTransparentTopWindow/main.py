# Demonstrate controlling desktop Windows brightness by putting a semi-transparent full-screen window in front of other windows.
#
# Apdapted From: https://stackoverflow.com/questions/550001/fully-transparent-windows-in-pygame
import pygame
import win32api
import win32con
import win32gui
import numpy as np


def main():

    pygame.init()

    # For borderless, use pygame.NOFRAME
    screen = pygame.display.set_mode((0, 0), pygame.NOFRAME, pygame.FULLSCREEN)

    fuchsia = (
        255,
        0,
        128,
    )  # Transparency color, demonstrate how to create full-transparency region inside window
    dark_red = (139, 0, 0)
    black = (0, 0, 0)

    # Create layered window
    hwnd = pygame.display.get_wm_info()["window"]
    win32gui.SetWindowLong(
        hwnd,
        win32con.GWL_EXSTYLE,
        win32gui.GetWindowLong(hwnd, win32con.GWL_EXSTYLE) | win32con.WS_EX_LAYERED,
    )

    AMP = 50
    arr = np.concatenate([np.arange(-AMP, AMP), np.arange(AMP, -AMP, -1)]) + AMP

    # Fill with black pygame
    screen.fill(black)
    pygame.draw.rect(screen, fuchsia, pygame.Rect(400, 200, 100, 100))
    pygame.draw.rect(screen, fuchsia, pygame.Rect(500, 300, 300, 200))
    pygame.display.update()

    i = 0
    done = False
    print("Press 'q' to exit.")
    while not done:

        for event in pygame.event.get():
            # If 'q' pressed or ESC pressed, quit
            if event.type == pygame.QUIT or (
                event.type == pygame.KEYDOWN and event.key == pygame.K_q
            ):
                done = True

        blend = int(arr[i % len(arr)])
        i += 1
        print("\rAlpha=", blend, end="")

        # Set window transparency color
        win32gui.SetLayeredWindowAttributes(
            hwnd,
            win32api.RGB(*fuchsia),
            blend,
            win32con.LWA_COLORKEY | win32con.LWA_ALPHA,
        )

        # pygame wait a while
        pygame.time.wait(15)


if __name__ == "__main__":
    main()
