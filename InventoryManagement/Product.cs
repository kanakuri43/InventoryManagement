using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManagement
{
    public class Product
    {
        public int Id { get; set; }
        public string Barcode { get; set; }
        public string Name { get; set; }
        public int CurrentStock { get; set; }
        public int MinimumStock { get; set; }
        public int? SupplierId { get; set; }
    }
}
