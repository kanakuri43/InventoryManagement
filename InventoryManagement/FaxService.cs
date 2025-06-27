using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Documents;
using System.Windows.Controls;
using System.IO.Packaging;
using System.Windows.Xps.Packaging;
using System.Windows.Markup;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using System.Windows.Xps;

namespace InventoryManagement
{
    public class FaxService
    {
        private readonly DatabaseManager _dbManager;

        public FaxService(DatabaseManager dbManager)
        {
            _dbManager = dbManager;
        }

        public void SendFax()
        {
            Console.Clear();
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("=== FAX送信 ===\n");
            Console.ResetColor();

            try
            {
                // 最低在庫数を下回った商品を取得
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

                // 仕入先ごとにグループ化して発注書を作成
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
                        // PDF発注書作成
                        string pdfPath = CreateOrderPDF(supplier, group.ToList());
                        // 発注履歴を記録
                        _dbManager.CreateOrderHistories(supplier.Id, group.ToList(), pdfPath, "自動発注");
                        successCount++;
                        Console.WriteLine($"✓ {supplier.Name} への発注書を作成しました。");
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

        private string CreateOrderPDF(Supplier supplier, List<Product> products)
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

                return pdfPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"PDF作成エラー: {ex.Message}");
            }
        }

    }
}