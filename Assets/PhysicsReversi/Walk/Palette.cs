using UnityEngine;

namespace PhysicsReversi.Walk
{
    // The only source of colour in the game (see ART.md). Materials, lights, sky and UI read from here.
    public static class Palette
    {
        public static readonly Color Table = Hex(0xF3E3C3);
        public static readonly Color BoardLight = Hex(0x9EDDC8);
        public static readonly Color BoardDark = Hex(0x6CC3AE);
        public static readonly Color StoneBlack = Hex(0x2E3350);
        public static readonly Color StoneWhite = Hex(0xFFF9EE);
        public static readonly Color PlayerBlack = Hex(0xF2665A);
        public static readonly Color PlayerWhite = Hex(0x4DA8E8);
        public static readonly Color Shadow = Hex(0x9990CC);
        public static readonly Color Outline = Hex(0x33284A);
        public static readonly Color Accent = Hex(0xFFC93C);
        public static readonly Color Face = Hex(0xFFE6CC);
        public static readonly Color TableEdge = Hex(0xE6CC9F);
        public static readonly Color RoomFloor = Hex(0xD9AE84);
        public static readonly Color Rug = Hex(0xD8CCF0);
        public static readonly Color Wall = Hex(0xF7E3D3);
        public static readonly Color Sun = Hex(0xFFF2DC);
        public static readonly Color SkyTop = Hex(0xBFE3F5);
        public static readonly Color SkyHorizon = Hex(0xFDF1DC);
        public static readonly Color GroundAmbient = Hex(0xE8C9A0);
        public static readonly Color BoardFrame = Hex(0x4E9E8B);
        public static readonly Color Haze = Hex(0xF4E2D0);

        // The colour each player's character and UI wear: 1 is black, 2 is white.
        public static Color Player(int playerId) => playerId == 2 ? PlayerWhite : PlayerBlack;

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
