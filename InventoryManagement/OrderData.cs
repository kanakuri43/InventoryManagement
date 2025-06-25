using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManagement
{
    public class OrderData
    {
        public DateTime OrderDate { get; set; }
        public string SupplierName { get; set; }
        public string SupplierFax { get; set; }
        public List<OrderItem> OrderItems { get; set; }
        public int TotalItems { get; set; }
    }
}
