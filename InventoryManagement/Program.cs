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

        [STAThread]
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            _dbManager = new DatabaseManager();
            _dbManager.Initialize();

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
                    SendFax();
                    break;
                case 2:
                    InputReceiving();
                    break;
                case 3:
                    ExportAllTablesToCSV();
                    break;
                case 4:
                    ProductManagement();
                    break;
                case 5:
                    SupplierManagement();
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

        static void ProductManagement()
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

        static void SendFax()
        {
            Console.Clear();
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("=== FAX送信 ===\n");
            Console.ResetColor();

            try
            {
                var lowStockProducts = _dbManager.GetLowStockProducts();

                if (lowStockProducts.Count == 0)
                {
                    Console.WriteLine("発注が必要な商品はありません。");
                    Console.WriteLine("何かキーを押してください...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"発注が必要な商品が {lowStockProducts.Count} 件見つかりました。");
                Console.WriteLine("\n発注対象商品:");
                foreach (var product in lowStockProducts)
                {
                    var supplier = _dbManager.GetSupplierById(product.SupplierId ?? 0);
                    string supplierName = supplier?.Name ?? "未設定";
                    Console.WriteLine($"- {product.Name} (現在:{product.CurrentStock}, 最低:{product.MinimumStock}) - 仕入先: {supplierName}");
                }

                Console.WriteLine("\nFAX送信を実行しますか？ (Y/N)");
                var confirm = Console.ReadKey(true);

                if (confirm.Key != ConsoleKey.Y)
                {
                    Console.WriteLine("キャンセルしました。");
                    Console.WriteLine("何かキーを押してください...");
                    Console.ReadKey();
                    return;
                }

                var supplierGroups = lowStockProducts
                    .Where(p => p.SupplierId.HasValue)
                    .GroupBy(p => p.SupplierId.Value)
                    .ToList();

                int successCount = 0;
                int totalCount = supplierGroups.Count;

                foreach (var group in supplierGroups)
                {
                    var supplier = _dbManager.GetSupplierById(group.Key);
                    if (supplier == null) continue;

                    try
                    {
                        string pdfPath = CreateOrderPDF(supplier, group.ToList());
                        _dbManager.CreateOrderHistories(supplier.Id, group.ToList(), pdfPath, "自動発注");
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ {supplier.Name} への発注処理でエラー: {ex.Message}");
                    }
                }

                Console.WriteLine($"\n完了: {successCount}/{totalCount} 件の発注書を送信しました。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAX送信処理でエラーが発生しました: {ex.Message}");
            }

            Console.WriteLine("何かキーを押してください...");
            Console.ReadKey();
        }

        static string CreateOrderPDF(Supplier supplier, List<Product> products)
        {
            string pdfDir = "pdf";
            if (!Directory.Exists(pdfDir))
            {
                Directory.CreateDirectory(pdfDir);
            }

            string fileName = $"order_{supplier.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string pdfPath = Path.Combine(pdfDir, fileName);
            string xpsPath = Path.Combine(pdfDir, $"temp_{supplier.Id}.xps");

            try
            {
                OrderSlip orderSlip = new OrderSlip();

                orderSlip.DataContext = new OrderSlipViewModel
                {
                    Supplier = supplier,
                    OrderDate = DateTime.Now
                };

                System.Windows.Documents.FixedPage fixedPage = new System.Windows.Documents.FixedPage();
                fixedPage.Width = 11.69 * 96;
                fixedPage.Height = 8.27 * 96;

                System.Windows.Controls.Canvas.SetLeft(orderSlip, 0);
                System.Windows.Controls.Canvas.SetTop(orderSlip, 0);
                orderSlip.Width = fixedPage.Width;
                orderSlip.Height = fixedPage.Height;

                fixedPage.Children.Add(orderSlip);

                PageContent pageContent = new PageContent();
                ((IAddChild)pageContent).AddChild(fixedPage);

                FixedDocument fixedDocument = new FixedDocument();
                fixedDocument.Pages.Add(pageContent);

                using (Package package = Package.Open(xpsPath, FileMode.Create))
                using (XpsDocument xpsDoc = new XpsDocument(package))
                {
                    XpsDocumentWriter writer = XpsDocument.CreateXpsDocumentWriter(xpsDoc);
                    writer.Write(fixedDocument.DocumentPaginator);
                }

                PdfSharp.Xps.XpsConverter.Convert(xpsPath, pdfPath, 0);

                if (File.Exists(xpsPath))
                {
                    File.Delete(xpsPath);
                }

                Console.WriteLine($"✓ PDF作成完了: {pdfPath}");
                return pdfPath;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        static void SupplierManagement()
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