using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace SlideViewer {
    public class TextAnnotation {
        private Point origin;
        public Point Origin {
            get { return origin; }
            set { origin = value; }
        }

        private Color color;
        public Color Color {
            get { return color; }
            set { color = value; }
        }

        private Font font;
        public Font Font {
            get { return font; }
            set { font = value; }
        }

        private String text;
        public String Text {
            get { return text; }
            set { text = value; }
        }

        private Guid id;
        public Guid Id {
            get { return id; }
            set { id = value; }
        }

        private int width;
        public int Width {
            get { return width; }
            set { width = value; }
        }

        private int height;
        public int Height {
            get { return height; }
            set { height = value; }
        } 

        public TextAnnotation(Guid id, String text, Color color, Font font, Point origin, int width, int height) {
            this.id = id;
            this.text = text;
            this.color = color;
            this.font = font;
            this.origin = origin;
            this.width = width;
            this.height = height;
        }
    }
}
