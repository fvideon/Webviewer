using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace SlideViewer {
    /// <summary>
    /// This is an image that the user added on the fly.
    /// </summary>
    public class DynamicImage {
        private Point origin;
        public Point Origin {
            get { return origin; }
            set { origin = value; }
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

        private Image image;
        public Image Img {
            get { return this.image; }
            set { this.image = value; }
        }

        public DynamicImage(Guid id, Point origin, int width, int height, Image image) {
            this.id = id;
            this.origin = origin;
            this.width = width;
            this.height = height;
            this.image = image;
        }
    }

    
}
