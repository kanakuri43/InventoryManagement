using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace InventoryManagement
{
    public class DatabaseManager
    {
        private SQLiteConnection _connection;
        private const string DbPath = "inventory.db";

        public void Initialize()
        {
            if (!File.Exists(DbPath))
            {
                SQLiteConnection.CreateFile(DbPath);
            }

            _connection = new SQLiteConnection($"Data Source={DbPath};Version=3;");
            _connection.Open();
            CreateTables();
        }

        public void Close()
        {
            _connection?.Close();
        }

        private void CreateTables()
        {
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
                    quantity INTEGER NOT NULL,
                    operation_type TEXT NOT NULL,
                    notes TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (product_id) REFERENCES products (id)
                );

                CREATE TABLE IF NOT EXISTS receiving_histories (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_id INTEGER NOT NULL,
                    quantity_change INTEGER NOT NULL,
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
                    quantity INTEGER NOT NULL,
                    status TEXT DEFAULT 'sent',
                    pdf_path TEXT,
                    notes TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (supplier_id) REFERENCES suppliers (id),
                    FOREIGN KEY (product_id) REFERENCES products (id)
                );            

                CREATE TABLE IF NOT EXISTS company_info (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    address TEXT,
                    tel TEXT,
                    fax TEXT
                );

            ";

            using (var command = new SQLiteCommand(createTables, _connection))
            {
                command.ExecuteNonQuery();
            }
        }

        // Product関連メソッド
        public Product GetProductByBarcode(string barcode)
        {
            string query = @"
                SELECT id, barcode, name, current_stock, minimum_stock, supplier_id 
                FROM products 
                WHERE barcode = @barcode";

            using (var command = new SQLiteCommand(query, _connection))
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

        public List<Product> GetLowStockProducts()
        {
            var products = new List<Product>();
            string query = @"
                SELECT id, barcode, name, current_stock, minimum_stock, supplier_id
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

        public void CreateProduct(string barcode, string name, int minimumStock, int supplierId)
        {
            string query = @"
                INSERT INTO products (barcode, name, minimum_stock, supplier_id) 
                VALUES (@barcode, @name, @minimumStock, @supplierId)";

            using (var command = new SQLiteCommand(query, _connection))
            {
                command.Parameters.AddWithValue("@barcode", barcode);
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@minimumStock", minimumStock);
                command.Parameters.AddWithValue("@supplierId", supplierId);
                command.ExecuteNonQuery();
            }
        }

        public void UpdateProduct(int id, string name, int minimumStock)
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

        public void UpdateStock(int productId, int quantity)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    string updateStock = "UPDATE products SET current_stock = @quantity WHERE id = @id";
                    using (var command = new SQLiteCommand(updateStock, _connection, transaction))
                    {
                        command.Parameters.AddWithValue("@quantity", quantity);
                        command.Parameters.AddWithValue("@id", productId);
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

        // Supplier関連メソッド
        public Supplier GetSupplierById(int supplierId)
        {
            string query = "SELECT * FROM suppliers WHERE id = @id";
            using (var command = new SQLiteCommand(query, _connection))
            {
                command.Parameters.AddWithValue("@id", supplierId);
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

        public void CreateSupplier(string name, int fax)
        {
            string query = "INSERT INTO suppliers (name, fax) VALUES (@name, @fax)";
            using (var command = new SQLiteCommand(query, _connection))
            {
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@fax", fax);
                command.ExecuteNonQuery();
            }
        }

        public void UpdateSupplier(int id, string name, int fax)
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

        // 履歴関連メソッド
        public void CreateInventoryHistory(int productId, int quantityChange, string operationType)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    string query = @"
                        INSERT INTO inventory_histories (product_id, quantity, operation_type) 
                        VALUES (@productId, @quantityChange, @operationType)";

                    using (var command = new SQLiteCommand(query, _connection, transaction))
                    {
                        command.Parameters.AddWithValue("@productId", productId);
                        command.Parameters.AddWithValue("@quantityChange", quantityChange);
                        command.Parameters.AddWithValue("@operationType", operationType);
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

        public void CreateReceivingHistory(int productId, int quantityChange)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    string query = @"
                        INSERT INTO receiving_histories (product_id, quantity_change, supplier_id) 
                        VALUES (@productId, @quantityChange, 1)";

                    using (var command = new SQLiteCommand(query, _connection, transaction))
                    {
                        command.Parameters.AddWithValue("@productId", productId);
                        command.Parameters.AddWithValue("@quantityChange", quantityChange);
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

        public void CreateOrderHistories(int supplierId, List<Product> products, string pdfPath, string notes = null)
        {
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    foreach (var product in products)
                    {
                        int orderQuantity = Math.Max(0, (product.MinimumStock * 2) - product.CurrentStock);

                        string query = @"
                            INSERT INTO order_histories (supplier_id, product_id, quantity, pdf_path, notes) 
                            VALUES (@supplierId, @productId, @orderQuantity, @pdfPath, @notes)";

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

        // CSV Export関連メソッド
        public List<string> GetAllTableNames()
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

        public SQLiteDataReader ExecuteReader(string query)
        {
            var command = new SQLiteCommand(query, _connection);
            return command.ExecuteReader();
        }
    }
}