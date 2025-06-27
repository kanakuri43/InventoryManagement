using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryManagement
{
    public class OrderSlipViewModel
    {
        public string Title { get; set; }
        public Supplier Supplier { get; set; }
        public DateTime OrderDate { get; set; }
        public List<OrderItem> OrderItems { get; set; }

        public string CompanyName { get; set; }
        public string CompanyAddress { get; set; }
        public string CompanyTel { get; set; }
        public string CompanyFax { get; set; }
    }
}
