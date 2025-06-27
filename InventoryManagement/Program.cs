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

namespace InventoryManagement
{
    class Program
    {
        private static SQLiteConnection _connection;
        private const string dbPath = "inventory.db";

        [STAThread]
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            InitializeDatabase();
            ShowMainMenu();
        }

        static void InitializeDatabase()
        {
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            _connection = new SQLiteConnection($"Data Source={dbPath};Version=3;");
            _connection.Open();

            // テーブル作成
            string createTables = @"
                CREATE TABLE IF NOT EXISTS suppliers (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    fax TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS products (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    barcode TEXT UNIQUE NOT NULL,
                    name TEXT NOT NULL,
                    current_stock INTEGER DEFAULT 0,
                    minimum_stock INTEGER DEFAULT 0,
                    supplier_id INTEGER,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (supplier_id) REFERENCES suppliers (id)
                );

                CREATE TABLE IF NOT EXISTS inventory_histories (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_id INTEGER NOT NULL,
                    quantity_change INTEGER NOT NULL,
                    operation_type TEXT NOT NULL,
                    notes TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (product_id) REFERENCES products (id)
                );

                CREATE TABLE IF NOT EXISTS receiving_histories (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_id INTEGER NOT NULL,
                    quantity INTEGER NOT NULL,
                    supplier_id INTEGER,
                    notes TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (product_id) REFERENCES products (id),
                    FOREIGN KEY (supplier_id) REFERENCES suppliers (id)
                );

                CREATE TABLE IF NOT EXISTS order_histories (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    supplier_id INTEGER NOT NULL,
                    product_id INTEGER NOT NULL,
                    order_quantity INTEGER NOT NULL,
                    status TEXT DEFAULT 'sent',
                    pdf_path TEXT,
                    notes TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (supplier_id) REFERENCES suppliers (id),
                    FOREIGN KEY (product_id) REFERENCES products (id)
                );            
            ";

            using (var command = new SQLiteCommand(createTables, _connection))
            {
                command.ExecuteNonQuery();
            }
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
                Console.BackgroundColor = ConsoleColor.Yellow;
                Console.ForegroundColor = ConsoleColor.Black;
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
                    // 在庫数入力
                    InputInventory();
                    break;
                case 1:
                    // FAX送信
                    SendFax();
                    break;
                case 2:
                    // 入荷入力
                    InputReceiving();
                    break;
                case 3:
                    // データ出力
                    ExportAllTablesToCSV();
                    break;
                case 4:
                    // 商品管理
                    ProductManagement();
                    break;
                case 5:
                    // 仕入先管理
                    SupplierManagement();
                    break;
                case 6:
                    Console.WriteLine("アプリケーションを終了します...");
                    _connection?.Close();
                    break;
            }
        }

        static void InputInventory()
        {
            while (true)
            {
                Console.Clear();
                Console.BackgroundColor = ConsoleColor.Yellow;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.WriteLine("=== 在庫入力 ===\n");
                Console.ResetColor();
                Console.WriteLine("バーコードを入力してください（x: 戻る）:");

                string barcode = Console.ReadLine();
                if (string.IsNullOrEmpty(barcode) || barcode.ToLower() == "x")
                    break;

                var product = GetProductByBarcode(barcode);
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
                        UpdateStock(product.Id, quantity, "在庫入力");
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
                Console.BackgroundColor = ConsoleColor.Yellow;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.WriteLine("=== 入荷入力 ===\n");
                Console.ResetColor();

                Console.WriteLine("バーコードを入力してください（x: 戻る）:");

                string barcode = Console.ReadLine();
                if (string.IsNullOrEmpty(barcode) || barcode.ToLower() == "x")
                    break;

                var product = GetProductByBarcode(barcode);
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
                        UpdateStock(product.Id, (product.CurrentStock + quantity), "在庫入力");
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
                Console.BackgroundColor = ConsoleColor.Yellow;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.WriteLine("=== 商品管理 ===\n");
                Console.ResetColor();
                Console.WriteLine("バーコードを入力してください（x: 戻る）:");

                string barcode = Console.ReadLine();
                if (string.IsNullOrEmpty(barcode) || barcode.ToLower() == "x")
                    break;

                var product = GetProductByBarcode(barcode);

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
                        CreateProduct(barcode, name, minStock);
                        Console.WriteLine("商品を新規登録しました。");
                    }
                    else
                    {
                        UpdateProduct(product.Id, name, minStock);
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
            Console.BackgroundColor = ConsoleColor.Yellow;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine("=== FAX送信 ===\n");
            Console.ResetColor();

            try
            {
                // 最低在庫数を下回った商品を取得
                var lowStockProducts = GetLowStockProducts();

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
                    var supplier = GetSupplierById(product.SupplierId ?? 0);
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

                // 仕入先ごとにグループ化して発注書を作成
                var supplierGroups = lowStockProducts
                    .Where(p => p.SupplierId.HasValue)
                    .GroupBy(p => p.SupplierId.Value)
                    .ToList();

                int successCount = 0;
                int totalCount = supplierGroups.Count;

                foreach (var group in supplierGroups)
                {
                    var supplier = GetSupplierById(group.Key);
                    if (supplier == null) continue;

                    try
                    {
                        // PDF発注書作成
                        string pdfPath = CreateOrderPDF(supplier, group.ToList());
                        // 発注履歴を記録
                        CreateOrderHistories(supplier.Id, group.ToList(), pdfPath, "自動発注");
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

        static void CreateOrderHistories(int supplierId, List<Product> products, string pdfPath, string notes = null)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    foreach (var product in products)
                    {
                        // 発注数量を計算（最低在庫数の2倍 - 現在在庫数）
                        int orderQuantity = Math.Max(0, (product.MinimumStock * 2) - product.CurrentStock);

                        string query = @"
                            INSERT INTO order_histories (
                                supplier_id, 
                                product_id, 
                                order_quantity, 
                                pdf_path, 
                                notes
                            ) VALUES (
                                @supplierId, 
                                @productId, 
                                @orderQuantity, 
                                @pdfPath, 
                                @notes
                            )";

                        using (var command = new SQLiteCommand(query, _connection, transaction))
                        {
                            command.Parameters.AddWithValue("@supplierId", supplierId);
                            command.Parameters.AddWithValue("@productId", product.Id);
                            command.Parameters.AddWithValue("@orderQuantity", orderQuantity);
                            command.Parameters.AddWithValue("@pdfPath", pdfPath ?? "");
                            command.Parameters.AddWithValue("@notes", notes ?? "");
                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
        static List<Product> GetLowStockProducts()
        {
            var products = new List<Product>();

            string query = @"
                SELECT 
                    id, barcode, name, current_stock, minimum_stock, supplier_id
                FROM products 
                WHERE current_stock <= minimum_stock
                ORDER BY supplier_id, name";

            using (var command = new SQLiteCommand(query, _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    products.Add(new Product
                    {
                        Id = reader.GetInt32("id"),
                        Barcode = reader.GetString("barcode"),
                        Name = reader.GetString("name"),
                        CurrentStock = reader.GetInt32("current_stock"),
                        MinimumStock = reader.GetInt32("minimum_stock"),
                        SupplierId = reader.IsDBNull("supplier_id") ? (int?)null : reader.GetInt32("supplier_id")
                    });
                }
            }

            return products;
        }

        static string CreateOrderPDF(Supplier supplier, List<Product> products)
        {
            // PDFディレクトリの作成
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
                // OrderSlipコントロールを作成
                OrderSlip orderSlip = new OrderSlip();

                // OrderSlipのデータコンテキストを設定（必要に応じて）
                orderSlip.DataContext = new OrderSlipViewModel
                {
                    Supplier = supplier,
                    OrderDate = DateTime.Now
                };

                // FixedPageを作成
                System.Windows.Documents.FixedPage fixedPage = new System.Windows.Documents.FixedPage();

                // A4横サイズに設定
                fixedPage.Width = 11.69 * 96;  // A4横幅 (792ポイント)
                fixedPage.Height = 8.27 * 96;  // A4横高さ (612ポイント)

                // OrderSlipをFixedPageに配置
                System.Windows.Controls.Canvas.SetLeft(orderSlip, 0);
                System.Windows.Controls.Canvas.SetTop(orderSlip, 0);
                orderSlip.Width = fixedPage.Width;
                orderSlip.Height = fixedPage.Height;

                fixedPage.Children.Add(orderSlip);

                // PageContentとFixedDocumentを作成
                PageContent pageContent = new PageContent();
                ((IAddChild)pageContent).AddChild(fixedPage);

                FixedDocument fixedDocument = new FixedDocument();
                fixedDocument.Pages.Add(pageContent);

                // XPSファイルとして保存
                using (Package package = Package.Open(xpsPath, FileMode.Create))
                using (XpsDocument xpsDoc = new XpsDocument(package))
                {
                    XpsDocumentWriter writer = XpsDocument.CreateXpsDocumentWriter(xpsDoc);
                    writer.Write(fixedDocument.DocumentPaginator);
                }

                // XPSをPDFに変換
                PdfSharp.Xps.XpsConverter.Convert(xpsPath, pdfPath, 0);

                // 一時XPSファイルを削除
                if (File.Exists(xpsPath))
                {
                    File.Delete(xpsPath);
                }

                Console.WriteLine($"✓ XAML使用PDF作成完了: {pdfPath}");
                return pdfPath;
                //return CreateOrderPDFWithXaml(supplier, products, pdfPath, xpsPath);
            }
            catch (Exception xamlEx)
            {
                return xamlEx.Message;
            }
        }


        static void SupplierManagement()
        {
            while (true)
            {
                Console.Clear();
                Console.BackgroundColor = ConsoleColor.Yellow;
                Console.ForegroundColor = ConsoleColor.Black;
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


                var suplier = GetSupplierById(supplierId);

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
                    if (suplier == null)
                    {
                        CreateSupplier(supplierId, supplierName, fax);
                        Console.WriteLine("商品を新規登録しました。");
                    }
                    else
                    {
                        UpdateSupplier(suplier.Id, supplierName, fax);
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

        static void ExportAllTablesToCSV()
        {
            try
            {
                // 出力ディレクトリを作成
                string outputDir = "csv_exports";
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // 全テーブル名を取得
                List<string> tableNames = GetAllTableNames();

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

        static List<string> GetAllTableNames()
        {
            List<string> tableNames = new List<string>();

            string sql = @"
            SELECT name FROM sqlite_master 
            WHERE type='table' 
            AND name NOT LIKE 'sqlite_%'
            ORDER BY name";

            using (SQLiteCommand command = new SQLiteCommand(sql, _connection))
            using (SQLiteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    tableNames.Add(reader["name"].ToString());
                }
            }

            return tableNames;
        }
        static void ExportTableToCSV(string tableName, string csvFilePath)
        {
            using (StreamWriter writer = new StreamWriter(csvFilePath, false, Encoding.UTF8))
            {
                string sql = $"SELECT * FROM [{tableName}]";

                using (SQLiteCommand command = new SQLiteCommand(sql, _connection))
                using (SQLiteDataReader reader = command.ExecuteReader())
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

            // カンマ、改行、ダブルクォートが含まれている場合はダブルクォートで囲む
            if (field.Contains(",") || field.Contains("\n") || field.Contains("\r") || field.Contains("\""))
            {
                // ダブルクォートをエスケープ（""に変換）
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }

            return field;
        }
        static Product GetProductByBarcode(string barcode)
        {
            string query = $@"
                SELECT 
                    id
                    , barcode
                    , name
                    , current_stock
                    , minimum_stock
                    , supplier_id 
                FROM
                    products 
                WHERE
                    barcode = '{barcode}'
                ";
            using (var command = new SQLiteCommand(query, _connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Product
                        {
                            Id = reader.GetInt32("id"),
                            Barcode = reader.GetString("barcode"),
                            Name = reader.GetString("name"),
                            CurrentStock = reader.GetInt32("current_stock"),
                            MinimumStock = reader.GetInt32("minimum_stock"),
                            SupplierId = reader.IsDBNull("supplier_id") ? (int?)null : reader.GetInt32("supplier_id")
                        };
                    }
                }
            }
            return null;
        }
        static Supplier GetSupplierById(int suplierId)
        {
            string query = $@"
                SELECT *
                FROM
                    suppliers 
                WHERE
                    id = {suplierId}
                ";
            using (var command = new SQLiteCommand(query, _connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Supplier
                        {
                            Id = reader.GetInt32("id"),
                            Name = reader.GetString("name"),
                            Fax = reader.GetString("fax")
                        };
                    }
                }
            }
            return null;
        }

        static void UpdateStock(int productId, int quantity, string operationType)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    // 在庫数更新
                    string updateStock = "UPDATE products SET current_stock = @quantity WHERE id = @id";
                    using (var command = new SQLiteCommand(updateStock, _connection, transaction))
                    {
                        command.Parameters.AddWithValue("@quantity", quantity);
                        command.Parameters.AddWithValue("@id", productId);
                        command.ExecuteNonQuery();
                    }
                    transaction.Commit();

                    // 履歴記録
                    CreateInventoryHistories(productId, quantity, "");
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        static void CreateInventoryHistories(int productId, int quantityChange, string operationType)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    string query = $@"
                        INSERT INTO
                            inventory_histories (
                                product_id
                                , quantity_change
                                , operation_type
                        ) VALUES (
                            {productId}
                            , {quantityChange}
                            , '{operationType}'
                        )
                    ";
                    using (var command = new SQLiteCommand(query, _connection, transaction))
                    {
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
        static void CreateReceivingHistories(int supplierId, List<Product> products, string pdfPath, string notes = null)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    foreach (var product in products)
                    {
                        // 発注数量を計算（最低在庫数の2倍 - 現在在庫数）
                        int orderQuantity = Math.Max(0, (product.MinimumStock * 2) - product.CurrentStock);

                        string query = @"
                            INSERT INTO order_histories (
                                supplier_id, 
                                product_id, 
                                order_quantity, 
                                pdf_path, 
                                notes
                            ) VALUES (
                                @supplierId, 
                                @productId, 
                                @orderQuantity, 
                                @pdfPath, 
                                @notes
                            )";

                        using (var command = new SQLiteCommand(query, _connection, transaction))
                        {
                            command.Parameters.AddWithValue("@supplierId", supplierId);
                            command.Parameters.AddWithValue("@productId", product.Id);
                            command.Parameters.AddWithValue("@orderQuantity", orderQuantity);
                            command.Parameters.AddWithValue("@pdfPath", pdfPath ?? "");
                            command.Parameters.AddWithValue("@notes", notes ?? "");
                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        static void CreateProduct(string barcode, string name, int minimumStock)
        {
            string query = @"INSERT INTO products (barcode, name, minimum_stock) 
                           VALUES (@barcode, @name, @minimumStock)";
            using (var command = new SQLiteCommand(query, _connection))
            {
                command.Parameters.AddWithValue("@barcode", barcode);
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@minimumStock", minimumStock);
                command.ExecuteNonQuery();
            }
        }

        static void UpdateProduct(int id, string name, int minimumStock)
        {
            string query = "UPDATE products SET name = @name, minimum_stock = @minimumStock WHERE id = @id";
            using (var command = new SQLiteCommand(query, _connection))
            {
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@minimumStock", minimumStock);
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }

        static void CreateSupplier(int id, string name, int fax)
        {
            string query = "INSERT INTO suppliers (name, fax) VALUES (@name, @fax)";
            using (var command = new SQLiteCommand(query, _connection))
            {
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@fax", fax);
                command.ExecuteNonQuery();
            }
        }
        static void UpdateSupplier(int id, string name, int fax)
        {
            string query = "UPDATE suppliers SET name = @name, fax = @fax WHERE id = @id";
            using (var command = new SQLiteCommand(query, _connection))
            {
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@fax", fax);
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }
    }



}