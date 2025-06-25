using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManagement
{
    public class OrderItem
    {
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public int OrderQuantity { get; set; }
    }
}
