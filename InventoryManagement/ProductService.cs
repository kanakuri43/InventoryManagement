using System;

namespace InventoryManagement
{
    public class ProductService
    {
        private readonly DatabaseManager _dbManager;

        public ProductService(DatabaseManager dbManager)
        {
            _dbManager = dbManager;
        }

        public void ProductManagement()
        {
            while (true)
            {
                Console.Clear();
                Console.BackgroundColor = ConsoleColor.Blue;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("=== 商品管理 ===\n");
                Console.ResetColor();
                Console.WriteLine("バーコードを入力してください（x: 戻る）:");

                string barcode = Console.ReadLine();
                if (string.IsNullOrEmpty(barcode) || barcode.ToLower() == "x")
                    break;

                var product = _dbManager.GetProductByBarcode(barcode);

                Console.WriteLine("商品名を入力してください:");
                string name = Console.ReadLine();

                Console.WriteLine("最低在庫数を入力してください:");
                if (!int.TryParse(Console.ReadLine(), out int minStock))
                {
                    Console.WriteLine("無効な数値です。");
                    continue;
                }

                Console.WriteLine("確定しますか？ (Y/N)");
                var confirm = Console.ReadKey(true);

                if (confirm.Key == ConsoleKey.Y)
                {
                    if (product == null)
                    {
                        _dbManager.CreateProduct(barcode, name, minStock, 1);
                        Console.WriteLine("商品を新規登録しました。");
                    }
                    else
                    {
                        _dbManager.UpdateProduct(product.Id, name, minStock);
                        Console.WriteLine("商品情報を更新しました。");
                    }
                }
                else
                {
                    Console.WriteLine("キャンセルしました。");
                }

                Console.WriteLine("何かキーを押してください...");
                Console.ReadKey();
            }
        }
    }
}