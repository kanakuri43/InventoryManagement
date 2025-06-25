using System;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace InventoryManagement
{
    class Program
    {
        private static SQLiteConnection connection;
        private const string dbPath = "inventory.db";

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            InitializeDatabase();
            ShowMainMenu();
        }

        static void InitializeDatabase()
        {
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            connection = new SQLiteConnection($"Data Source={dbPath};Version=3;");
            connection.Open();

            // テーブル作成
            string createTables = @"
                CREATE TABLE IF NOT EXISTS suppliers (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    contact TEXT,
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

                CREATE TABLE IF NOT EXISTS stock_history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_id INTEGER NOT NULL,
                    quantity_change INTEGER NOT NULL,
                    operation_type TEXT NOT NULL,
                    notes TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (product_id) REFERENCES products (id)
                );

                CREATE TABLE IF NOT EXISTS receiving_history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_id INTEGER NOT NULL,
                    quantity INTEGER NOT NULL,
                    supplier_id INTEGER,
                    notes TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (product_id) REFERENCES products (id),
                    FOREIGN KEY (supplier_id) REFERENCES suppliers (id)
                );
            ";

            using (var command = new SQLiteCommand(createTables, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        static void ShowMainMenu()
        {
            int selectedIndex = 0;
            string[] menuItems = {
                "1. 在庫入力",
                "2. 商品管理",
                "3. FAX送信",
                "4. 入荷入力",
                "5. 仕入先管理",
                "6. 終了"
            };

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== 在庫管理システム ===\n");

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
                        if (selectedIndex == 5) return; // 終了
                        break;
                }
            }
        }

        static void ExecuteMenuOption(int option)
        {
            switch (option)
            {
                case 0:
                    StockInput();
                    break;
                case 1:
                    ProductManagement();
                    break;
                case 2:
                    FaxSend();
                    break;
                case 3:
                    ReceivingInput();
                    break;
                case 4:
                    SupplierManagement();
                    break;
                case 5:
                    Console.WriteLine("アプリケーションを終了します...");
                    connection?.Close();
                    break;
            }
        }

        static void StockInput()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== 在庫入力 ===\n");
                Console.WriteLine("バーコードを入力してください（終了: 'exit'）:");

                string barcode = Console.ReadLine();
                if (string.IsNullOrEmpty(barcode) || barcode.ToLower() == "exit")
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
                    Console.WriteLine($"在庫を {quantity} 個追加しますか？ (Y/N)");
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

        static void ProductManagement()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== 商品管理 ===\n");
                Console.WriteLine("バーコードを入力してください（終了: 'exit'）:");

                string barcode = Console.ReadLine();
                if (string.IsNullOrEmpty(barcode) || barcode.ToLower() == "exit")
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

        static void FaxSend()
        {
            Console.Clear();
            Console.WriteLine("=== FAX送信 ===\n");

            // FAX送信処理をここに実装
            SendFax();

            Console.WriteLine("何かキーを押してください...");
            Console.ReadKey();
        }

        static void ReceivingInput()
        {
            Console.Clear();
            Console.WriteLine("=== 入荷入力 ===\n");
            Console.WriteLine("入荷処理を実行しますか？ (Y/N)");

            var confirm = Console.ReadKey(true);
            if (confirm.Key == ConsoleKey.Y)
            {
                // 入荷処理の実装
                ProcessReceiving();
                Console.WriteLine("入荷処理を実行しました。");
            }
            else
            {
                Console.WriteLine("キャンセルしました。");
            }

            Console.WriteLine("何かキーを押してください...");
            Console.ReadKey();
        }

        static void SupplierManagement()
        {
            Console.Clear();
            Console.WriteLine("=== 仕入先管理 ===\n");

            Console.WriteLine("仕入先名を入力してください:");
            string name = Console.ReadLine();

            Console.WriteLine("連絡先を入力してください:");
            string contact = Console.ReadLine();

            Console.WriteLine("登録しますか？ (Y/N)");
            var confirm = Console.ReadKey(true);

            if (confirm.Key == ConsoleKey.Y)
            {
                CreateSupplier(name, contact);
                Console.WriteLine("仕入先を登録しました。");
            }
            else
            {
                Console.WriteLine("キャンセルしました。");
            }

            Console.WriteLine("何かキーを押してください...");
            Console.ReadKey();
        }

        static Product GetProductByBarcode(string barcode)
        {
            string query = "SELECT id, barcode, name, current_stock, minimum_stock, supplier_id FROM products WHERE barcode = @barcode";
            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@barcode", barcode);
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

        static void UpdateStock(int productId, int quantityChange, string operationType)
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // 在庫数更新
                    string updateStock = "UPDATE products SET current_stock = current_stock + @quantity WHERE id = @id";
                    using (var command = new SQLiteCommand(updateStock, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@quantity", quantityChange);
                        command.Parameters.AddWithValue("@id", productId);
                        command.ExecuteNonQuery();
                    }

                    // 履歴記録
                    string insertHistory = @"INSERT INTO stock_history (product_id, quantity_change, operation_type) 
                                           VALUES (@productId, @quantity, @operation)";
                    using (var command = new SQLiteCommand(insertHistory, connection, transaction))
                    {
                        command.Parameters.AddWithValue("@productId", productId);
                        command.Parameters.AddWithValue("@quantity", quantityChange);
                        command.Parameters.AddWithValue("@operation", operationType);
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

        static void CreateProduct(string barcode, string name, int minimumStock)
        {
            string query = @"INSERT INTO products (barcode, name, minimum_stock) 
                           VALUES (@barcode, @name, @minimumStock)";
            using (var command = new SQLiteCommand(query, connection))
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
            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@minimumStock", minimumStock);
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }

        static void CreateSupplier(string name, string contact)
        {
            string query = "INSERT INTO suppliers (name, contact) VALUES (@name, @contact)";
            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@contact", contact);
                command.ExecuteNonQuery();
            }
        }

        static void SendFax()
        {
            // FAX送信の実装（外部ライブラリまたはAPIを使用）
            Console.WriteLine("FAX送信機能を呼び出しています...");
            Console.WriteLine("FAX送信が完了しました。");
        }

        static void ProcessReceiving()
        {
            // 入荷処理の実装
            Console.WriteLine("入荷処理を実行中...");
            // 実際の入荷処理ロジックをここに実装
        }
    }



}