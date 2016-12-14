using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Objects
{
    public class ProductFormat
    {
        public readonly int ProductFormatId;
        public readonly int Width;

        public ProductFormat(int productformatid, int width)
        {
            this.ProductFormatId = productformatid;
            this.Width = width;
        }
    }
}
