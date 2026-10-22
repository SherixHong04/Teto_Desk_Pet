using System;
using System.Windows;
using System.Windows.Media;

namespace TetoDesk {
    public sealed class DragMotion {
        private Point origin, previous;
        public Vector Offset { get; private set; }
        public bool Dragging { get; private set; }
        public int Direction { get; private set; }
        public string Animation { get { return Direction < 0 ? "running-left" : Direction > 0 ? "running-right" : ""; } }
        public void Begin(Point pointer) { origin = previous = pointer; Offset = new Vector(); Dragging = false; Direction = 0; }
        public void Move(Point pointer, Matrix fromDevice) {
            Vector movement = fromDevice.Transform(pointer - previous); previous = pointer;
            Offset = fromDevice.Transform(pointer - origin);
            Dragging |= Offset.Length > 5;
            if (!Dragging) return;
            // Direction follows the latest pointer movement, including reversals
            // that remain on the same side of the original press position.
            if (movement.X <= -0.5) Direction = -1;
            else if (movement.X >= 0.5) Direction = 1;
        }
    }
}
