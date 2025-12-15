using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;

namespace DoAn
{
    public class InvoicePrinter
    {
        public class ServiceLine
        {
            public string TenDV { get; set; }
            public decimal SoLuong { get; set; }
            public decimal DonGia { get; set; }
            public decimal ThanhTien { get; set; }
        }

        public class InvoicePrintModel
        {
            public string TenKhach { get; set; }
            public string SoPhong { get; set; }
            public DateTime NgayThue { get; set; }
            public DateTime NgayTra { get; set; }
            public decimal RoomTotal { get; set; }
            public decimal RoomOnly { get; set; }
            public List<ServiceLine> Services { get; set; } = new List<ServiceLine>();
        }

        // Render trả về y cuối cùng; nếu không đủ chỗ (để vẽ toàn bộ invoice) thì overflow = true và không vẽ gì => caller phải new page.
        public static int Render(PrintPageEventArgs e, InvoicePrintModel model, int startY, int pageBottom, out bool overflow)
        {
            overflow = false;
            if (e == null || e.Graphics == null) throw new ArgumentNullException(nameof(e));

            // Defensive: ensure model not null
            model = model ?? new InvoicePrintModel { TenKhach = "", SoPhong = "", NgayThue = DateTime.Now, NgayTra = DateTime.Now };

            Graphics g = e.Graphics;

            // Fonts (dispose after use)
            Font titleFont = null;
            Font headerFont = null;
            Font textFont = null;
            try
            {
                try
                {
                    titleFont = new Font("Arial", 16, FontStyle.Bold);
                    headerFont = new Font("Arial", 14, FontStyle.Bold);
                    textFont = new Font("Arial", 12);
                }
                catch
                {
                    // fallback if font not available
                    titleFont = SystemFonts.DefaultFont;
                    headerFont = SystemFonts.DefaultFont;
                    textFont = SystemFonts.DefaultFont;
                }

                // layout
                int left = 60;
                int pageWidth = Math.Max(300, e.PageBounds.Width); // defensive min width
                int right = pageWidth - 60;
                if (right <= left + 50) right = left + 300; // ensure reasonable right bound
                int y = startY;

                int lineHeight = (int)Math.Ceiling(textFont.GetHeight(g)) + 6; // compute based on font
                int gapSmall = 6;
                int gapMedium = 12;

                // column widths (defensive)
                int amountWidth = 120;
                int donGiaWidth = 110;
                int slWidth = 50;
                int paddingBetweenCols = 10;
                int reservedRight = amountWidth + donGiaWidth + slWidth + paddingBetweenCols * 3;
                int serviceNameWidth = Math.Max(100, (right - left) - reservedRight);

                int colSL_X = left + serviceNameWidth + paddingBetweenCols;
                int colDonGia_X = colSL_X + slWidth + paddingBetweenCols;
                int colThanhTien_X = colDonGia_X + donGiaWidth + paddingBetweenCols;

                // StringFormats
                using (var rightAlign = new StringFormat() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Near })
                using (var leftAlign = new StringFormat() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
                {
                    // ===== TIÊU ĐỀ =====
                    g.DrawString("KHÁCH SẠN ĐÂU CŨNG ĐƯỢC", titleFont, Brushes.Black, left + 100, y);
                    y += lineHeight + 2;
                    g.DrawString("HÓA ĐƠN TRẢ PHÒNG", headerFont, Brushes.Black, left + 180, y);
                    y += lineHeight + gapMedium;

                    // ===== THÔNG TIN KHÁCH =====
                    g.DrawString($"Tên khách hàng: {model.TenKhach}", textFont, Brushes.Black, new RectangleF(left, y, serviceNameWidth, lineHeight), leftAlign); y += lineHeight;
                    g.DrawString($"Số phòng: {model.SoPhong}", textFont, Brushes.Black, new RectangleF(left, y, serviceNameWidth, lineHeight), leftAlign); y += lineHeight;
                    g.DrawString($"Ngày thuê: {model.NgayThue:dd/MM/yyyy HH:mm}", textFont, Brushes.Black, new RectangleF(left, y, serviceNameWidth, lineHeight), leftAlign); y += lineHeight;
                    g.DrawString($"Ngày trả: {model.NgayTra:dd/MM/yyyy HH:mm}", textFont, Brushes.Black, new RectangleF(left, y, serviceNameWidth, lineHeight), leftAlign);
                    y += lineHeight + gapMedium;

                    // ===== DANH SÁCH DỊCH VỤ =====
                    g.DrawString("DANH SÁCH DỊCH VỤ", headerFont, Brushes.Black, new RectangleF(left, y, serviceNameWidth, lineHeight), leftAlign);
                    y += lineHeight;

                    // Header columns
                    g.DrawString("Dịch vụ", headerFont, Brushes.Black, new RectangleF(left, y, serviceNameWidth, lineHeight), leftAlign);
                    g.DrawString("SL", headerFont, Brushes.Black, new RectangleF(colSL_X, y, slWidth, lineHeight), rightAlign);
                    g.DrawString("Đơn giá", headerFont, Brushes.Black, new RectangleF(colDonGia_X, y, donGiaWidth, lineHeight), rightAlign);
                    g.DrawString("Thành tiền", headerFont, Brushes.Black, new RectangleF(colThanhTien_X, y, amountWidth, lineHeight), rightAlign);
                    y += lineHeight;

                    decimal totalService = 0;

                    var services = model.Services ?? Enumerable.Empty<ServiceLine>();
                    // Filter and keep order
                    var validServices = services.Where(s => !string.IsNullOrWhiteSpace(s.TenDV)).ToList();

                    if (!validServices.Any())
                    {
                        g.DrawString("(Không có dịch vụ)", textFont, Brushes.Black, new RectangleF(left, y, serviceNameWidth, lineHeight), leftAlign);
                        y += lineHeight;
                    }
                    else
                    {
                        foreach (var s in validServices)
                        {
                            // Ensure we have space: at least one line for current + footer room detail (conservative)
                            if (y + lineHeight * 6 > pageBottom)
                            {
                                overflow = true;
                                return y;
                            }

                            string donGiaText = (s.DonGia > 0) ? s.DonGia.ToString("N0") + " VND" : "";
                            string thanhTienText = (s.ThanhTien > 0) ? s.ThanhTien.ToString("N0") + " VND" : "";

                            // service name may wrap: draw in rectangle of serviceNameWidth
                            g.DrawString(s.TenDV.Trim(), textFont, Brushes.Black, new RectangleF(left, y, serviceNameWidth, lineHeight), leftAlign);
                            g.DrawString(s.SoLuong.ToString("0.##"), textFont, Brushes.Black, new RectangleF(colSL_X, y, slWidth, lineHeight), rightAlign);
                            g.DrawString(donGiaText, textFont, Brushes.Black, new RectangleF(colDonGia_X, y, donGiaWidth, lineHeight), rightAlign);
                            g.DrawString(thanhTienText, textFont, Brushes.Black, new RectangleF(colThanhTien_X, y, amountWidth, lineHeight), rightAlign);

                            y += lineHeight;
                            totalService += s.ThanhTien;
                        }
                    }

                    // ===== NGANG PHÂN CÁCH =====
                    y += gapSmall;
                    g.DrawLine(Pens.Black, left, y, right, y);
                    y += gapMedium;

                    // ===== CHI TIẾT THANH TOÁN =====
                    g.DrawString("CHI TIẾT THANH TOÁN", headerFont, Brushes.Black, new RectangleF(left, y, serviceNameWidth, lineHeight), leftAlign);
                    y += lineHeight + 2;

                    // Tiền phòng (room only) và tiền dịch vụ
                    g.DrawString("Tiền phòng:", textFont, Brushes.Black, new RectangleF(left, y, serviceNameWidth, lineHeight), leftAlign);
                    g.DrawString((model.RoomOnly).ToString("N0") + " VND", textFont, Brushes.Black, new RectangleF(colThanhTien_X, y, amountWidth, lineHeight), rightAlign);
                    y += lineHeight;

                    g.DrawString("Tiền dịch vụ:", textFont, Brushes.Black, new RectangleF(left, y, serviceNameWidth, lineHeight), leftAlign);
                    g.DrawString(totalService.ToString("N0") + " VND", textFont, Brushes.Black, new RectangleF(colThanhTien_X, y, amountWidth, lineHeight), rightAlign);
                    y += lineHeight;

                    // separator
                    y += gapSmall;
                    g.DrawLine(Pens.Black, left, y, right, y);
                    y += gapMedium;

                    // Tổng tiền
                    decimal tongTien = model.RoomOnly + totalService;
                    g.DrawString("TỔNG TIỀN:", headerFont, Brushes.Black, new RectangleF(left, y, serviceNameWidth, lineHeight), leftAlign);
                    g.DrawString(tongTien.ToString("N0") + " VND", headerFont, Brushes.Black, new RectangleF(colThanhTien_X, y, amountWidth, lineHeight), rightAlign);
                    y += lineHeight + gapMedium;

                    // LỜI CẢM ƠN (chỉ in một lần)
                    g.DrawString("Xin cảm ơn quý khách! Hẹn gặp lại!", textFont, Brushes.Black, new RectangleF(left + 80, y, serviceNameWidth, lineHeight), leftAlign);
                    y += lineHeight;

                    // Cuối bill: nhẹ nhàng một gạch tách và khoảng cách để next invoice
                    y += gapSmall;
                    g.DrawLine(Pens.Gray, left, y, right, y);
                    y += gapMedium; // khoảng cách giữa 2 hóa đơn

                    overflow = y > pageBottom;
                    return y;
                } // end using StringFormat
            }
            finally
            {
                // dispose fonts if we created them
                if (titleFont != null && titleFont != SystemFonts.DefaultFont) titleFont.Dispose();
                if (headerFont != null && headerFont != SystemFonts.DefaultFont) headerFont.Dispose();
                if (textFont != null && textFont != SystemFonts.DefaultFont) textFont.Dispose();
            }
        }

        public static void Render(PrintPageEventArgs e, InvoicePrintModel m)
        {
            bool ov;
            Render(e, m, 50, e.PageBounds.Height - 50, out ov);
        }
        public static int EstimateHeight(InvoicePrintModel model)
        {
            int baseHeight = 200; // tiêu đề + thông tin khách
            int serviceHeight = (model.Services?.Where(s => !string.IsNullOrWhiteSpace(s.TenDV)).Count() ?? 0) * 24;
            int footerHeight = 140; // phần tổng cộng + lời cảm ơn
            return baseHeight + serviceHeight + footerHeight;
        }

    }
}