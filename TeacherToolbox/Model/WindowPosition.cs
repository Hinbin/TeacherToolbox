using System;

namespace TeacherToolbox.Model
{
    /// <summary>
    /// Structure representing the position and size of a window
    /// </summary>
    public struct WindowPosition
    {
        /// <summary>
        /// X coordinate of the window
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Y coordinate of the window
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Width of the window
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Height of the window
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// ID of the display containing the window
        /// </summary>
        public ulong DisplayID { get; set; }

        /// <summary>
        /// Creates a new window position instance
        /// </summary>
        public WindowPosition(int x, int y, double width, double height, ulong displayId)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            DisplayID = displayId;
        }

        /// <summary>
        /// Determines if this window position is empty or contains valid data
        /// </summary>
        public bool IsEmpty => Width <= 0 || Height <= 0;

        /// <summary>
        /// Creates an empty window position
        /// </summary>
        public static WindowPosition Empty => new WindowPosition(0, 0, 0, 0, 0);
    }
}