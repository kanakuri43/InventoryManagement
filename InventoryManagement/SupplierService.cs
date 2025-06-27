using System;

namespace InventoryManagement
{
    public class SupplierService
    {
        private readonly DatabaseManager _dbManager;

        public SupplierService(DatabaseManager dbManager)
        {
            _dbManager = dbManager;
        }

        public void SupplierManagement()
        {
            while (true)
            {
                Console.Clear();
                Console.BackgroundColor = ConsoleColor.Blue;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("=== 仕入先管理 ===\n");
                Console.ResetColor();
                Console.WriteLine("仕入先コードを入力してください（x: 戻る）:");

                string input = Console.ReadLine();
                if (string.IsNullOrEmpty(input) || input.ToLower() == "x")
                    break;

                if (!int.TryParse(input, out int supplierId))
                {
                    Console.WriteLine("無効な数値です。");
                    continue;
                }

                var supplier = _dbManager.GetSupplierById(supplierId);

                Console.WriteLine("仕入先名を入力してください:");
                string supplierName = Console.ReadLine();

                Console.WriteLine("FAX番号を入力してください（ハイフンなし）:");
                if (!int.TryParse(Console.ReadLine(), out int fax))
                {
                    Console.WriteLine("無効な数値です。");
                    continue;
                }

                Console.WriteLine("確定しますか？ (Y/N)");
                var confirm = Console.ReadKey(true);

                if (confirm.Key == ConsoleKey.Y)
                {
                    if (supplier == null)
                    {
                        _dbManager.CreateSupplier(supplierName, fax);
                        Console.WriteLine("仕入先を新規登録しました。");
                    }
                    else
                    {
                        _dbManager.UpdateSupplier(supplier.Id, supplierName, fax);
                        Console.WriteLine("仕入先情報を更新しました。");
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