using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.IO.Packaging;
using System.Text;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using System.Windows.Controls;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using System.Collections.Generic;
using System.Linq;

namespace InventoryManagement
{
    class Program
    {
        private static DatabaseManager _dbManager;
        private static FaxService _faxService;
        private static ProductService _productService;
        private static SupplierService _supplierService;

        [STAThread]
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            _dbManager = new DatabaseManager();
            _dbManager.Initialize();

            _faxService = new FaxService(_dbManager);
            _productService = new ProductService(_dbManager);
            _supplierService = new SupplierService(_dbManager);

            ShowMainMenu();

            _dbManager.Close();
        }

        static void ShowMainMenu()
        {
            int selectedIndex = 0;
            string[] menuItems = {
                "- 在庫入力",
                "- FAX送信",
                "- 入荷入力",
                "- データ出力",
                "- 商品管理",
                "- 仕入先管理",
                "- 終了"
            };

            while (true)
            {
                Console.Clear();
                Console.BackgroundColor = ConsoleColor.Blue;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("=== 在庫管理システム ===\n");
                Console.ResetColor();

                for (int i = 0; i < menuItems.Length; i++)
                {
                    if (i == selectedIndex)
                    {
                        Console.BackgroundColor = ConsoleColor.Gray;
                        Console.ForegroundColor = ConsoleColor.Black;
                    }
                    Console.WriteLine(menuItems[i]);
                    Console.ResetColor();
                }

                Console.WriteLine("\n上下キーで選択、Enterで決定");

                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                switch (keyInfo.Key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex = (selectedIndex - 1 + menuItems.Length) % menuItems.Length;
                        break;
                    case ConsoleKey.DownArrow:
                        selectedIndex = (selectedIndex + 1) % menuItems.Length;
                        break;
                    case ConsoleKey.Enter:
                        ExecuteMenuOption(selectedIndex);
                        if (selectedIndex == 6) return; // 終了
                        break;
                }
            }
        }

        static void ExecuteMenuOption(int option)
        {
            switch (option)
            {
                case 0:
                    InputInventory();
                    break;
                case 1:
                    _faxService.SendFax();
                    break;
                case 2:
                    InputReceiving();
                    break;
                case 3:
                    ExportAllTablesToCSV();
                    break;
                case 4:
                    _productService.ProductManagement();
                    break;
                case 5:
                    _supplierService.SupplierManagement();
                    break;
                case 6:
                    Console.WriteLine("アプリケーションを終了します...");
                    break;
            }
        }

        static void InputInventory()
        {
            while (true)
            {
                Console.Clear();
                Console.BackgroundColor = ConsoleColor.Blue;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("=== 在庫入力 ===\n");
                Console.ResetColor();
                Console.WriteLine("バーコードを入力してください（x: 戻る）:");

                string barcode = Console.ReadLine();
                if (string.IsNullOrEmpty(barcode) || barcode.ToLower() == "x")
                    break;

                var product = _dbManager.GetProductByBarcode(barcode);
                if (product == null)
                {
                    Console.WriteLine("商品が見つかりません。商品管理で登録してください。");
                    Console.WriteLine("何かキーを押してください...");
                    Console.ReadKey();
                    continue;
                }

                Console.WriteLine($"商品名: {product.Name}");
                Console.WriteLine($"現在の在庫: {product.CurrentStock}");
                Console.WriteLine("数量を入力してください:");

                if (int.TryParse(Console.ReadLine(), out int quantity))
                {
                    Console.WriteLine($"在庫を更新します {quantity}  (Y/N)");
                    var confirm = Console.ReadKey(true);

                    if (confirm.Key == ConsoleKey.Y)
                    {
                        _dbManager.UpdateStock(product.Id, quantity);
                        _dbManager.CreateInventoryHistory(product.Id, quantity, "在庫入力");

                        Console.WriteLine("在庫を更新しました。");
                    }
                    else
                    {
                        Console.WriteLine("キャンセルしました。");
                    }
                }
                else
                {
                    Console.WriteLine("無効な数量です。");
                }

                Console.WriteLine("何かキーを押してください...");
                Console.ReadKey();
            }
        }

        static void InputReceiving()
        {
            while (true)
            {
                Console.Clear();
                Console.BackgroundColor = ConsoleColor.Blue;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("=== 入荷入力 ===\n");
                Console.ResetColor();

                Console.WriteLine("バーコードを入力してください（x: 戻る）:");

                string barcode = Console.ReadLine();
                if (string.IsNullOrEmpty(barcode) || barcode.ToLower() == "x")
                    break;

                var product = _dbManager.GetProductByBarcode(barcode);
                if (product == null)
                {
                    Console.WriteLine("商品が見つかりません。商品管理で登録してください。");
                    Console.WriteLine("何かキーを押してください...");
                    Console.ReadKey();
                    continue;
                }

                Console.WriteLine("数量を入力してください:");
                if (int.TryParse(Console.ReadLine(), out int quantity))
                {
                    Console.WriteLine($"在庫を更新します {quantity} 追加 (Y/N)");
                    var confirm = Console.ReadKey(true);

                    if (confirm.Key == ConsoleKey.Y)
                    {
                        _dbManager.UpdateStock(product.Id, (product.CurrentStock + quantity));
                        _dbManager.CreateReceivingHistory(product.Id, quantity);

                        Console.WriteLine("在庫を更新しました。");
                    }
                    else
                    {
                        Console.WriteLine("キャンセルしました。");
                    }
                }
                else
                {
                    Console.WriteLine("無効な数量です。");
                }
            }
        }

        static void ExportAllTablesToCSV()
        {
            try
            {
                string outputDir = "csv_exports";
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                List<string> tableNames = _dbManager.GetAllTableNames();

                if (tableNames.Count == 0)
                {
                    Console.WriteLine("テーブルが見つかりませんでした。");
                    return;
                }

                Console.WriteLine($"見つかったテーブル数: {tableNames.Count}");

                int exportedCount = 0;
                foreach (string tableName in tableNames)
                {
                    try
                    {
                        string csvFilePath = Path.Combine(outputDir, $"{tableName}.csv");
                        ExportTableToCSV(tableName, csvFilePath);
                        Console.WriteLine($"✓ {tableName} → {csvFilePath}");
                        exportedCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ {tableName} のエクスポートに失敗: {ex.Message}");
                    }
                }

                Console.WriteLine($"\n完了: {exportedCount}/{tableNames.Count} テーブルをエクスポートしました。");
                Console.WriteLine("何かキーを押してください...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラー: {ex.Message}");
            }
        }

        static void ExportTableToCSV(string tableName, string csvFilePath)
        {
            using (StreamWriter writer = new StreamWriter(csvFilePath, false, Encoding.UTF8))
            {
                string sql = $"SELECT * FROM [{tableName}]";

                using (SQLiteDataReader reader = _dbManager.ExecuteReader(sql))
                {
                    // ヘッダー行を書き込み
                    List<string> columnNames = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columnNames.Add(reader.GetName(i));
                    }
                    writer.WriteLine(string.Join(",", columnNames.ConvertAll(EscapeCsvField)));

                    // データ行を書き込み
                    while (reader.Read())
                    {
                        List<string> values = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            object value = reader[i];
                            string stringValue = value == DBNull.Value ? "" : value.ToString();
                            values.Add(EscapeCsvField(stringValue));
                        }
                        writer.WriteLine(string.Join(",", values));
                    }
                }
            }
        }

        static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            if (field.Contains(",") || field.Contains("\n") || field.Contains("\r") || field.Contains("\""))
            {
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }

            return field;
        }
    }
}