using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.Entity;
using System.Drawing;
using System.Drawing.Printing;
using static DoAn.Model1;

namespace DoAn
{
    public partial class Form2 : Form
    {
        private List<InvoicePrinter.InvoicePrintModel> invoicesToPrint = null;
        private int invoicePrintIndex = 0;

        private Color tabSelectedColor = ColorTranslator.FromHtml("#2A5980"); 
        private Color tabUnselectedColor = ColorTranslator.FromHtml("#4682B4");

        // Helper: tạo InvoicePrintModel cho một phòng/khách (dùng DB để lấy dịch vụ)
        private InvoicePrinter.InvoicePrintModel PrepareInvoiceModel(string tenKH, string soPhong)
        {
            var model = new InvoicePrinter.InvoicePrintModel
            {
                TenKhach = tenKH ?? "",
                SoPhong = soPhong ?? "",
                NgayThue = dtpNgayThue != null ? dtpNgayThue.Value : DateTime.Now,
                NgayTra = dtpTraP != null ? dtpTraP.Value : DateTime.Now
            };

            try
            {
                if (string.IsNullOrWhiteSpace(soPhong)) return model;

                // Tìm khách (ưu tiên match tên + phòng, fallback theo phòng)
                KhachHang kh = null;
                if (!string.IsNullOrWhiteSpace(tenKH))
                {
                    kh = db.KhachHangs.Where(k => k.SoPhong == soPhong && k.TenKhachHang == tenKH)
                                      .OrderByDescending(k => k.NgayThue).FirstOrDefault();
                }
                if (kh == null)
                {
                    kh = db.KhachHangs.Where(k => k.SoPhong == soPhong)
                                      .OrderByDescending(k => k.NgayThue).FirstOrDefault();
                }

                // Lấy dịch vụ đã lưu trong DB cho khách (nếu có)
                if (kh != null)
                {
                    var dbItems = db.ChiTietSuDungDVs
                                    .Where(ct => ct.MaKhachHang == kh.STT)
                                    .Include(ct => ct.DichVu)
                                    .ToList();

                    foreach (var it in dbItems)
                    {
                        model.Services.Add(new InvoicePrinter.ServiceLine
                        {
                            TenDV = it.DichVu?.TenDV ?? ("DV#" + it.MaDV),
                            SoLuong = it.SoLuong,
                            DonGia = it.DichVu?.DonGia ?? 0m,
                            ThanhTien = it.ThanhTien
                        });
                    }
                }

                // Lấy tiền phòng từ DB
                var phong = db.Phongs.FirstOrDefault(p => p.SoPhong == soPhong);
                decimal roomTotal = phong?.Gia ?? 0m;
                decimal servicesTotal = model.Services.Sum(s => s.ThanhTien);
                decimal roomOnly = roomTotal - servicesTotal;
                if (roomOnly < 0) roomOnly = Math.Max(0, roomTotal);

                model.RoomTotal = roomTotal;
                model.RoomOnly = roomOnly;
            }
            catch
            {
                // ignore - return whatever collected
            }

            return model;
        }

        PrintDocument printDocument = new PrintDocument();
        private readonly Model1 db = new Model1();
        private int? currentMaKhachHang = null; // Lưu STT (ID) của khách hàng đang được chọn
        private List<DichVu> allDichVuList;
        private List<ChiTietSuDungDV> danhSachDvMoiThem = new List<ChiTietSuDungDV>();
        private DataGridViewRow rowBeingEdited = null;

        // ======================== TAB 1 - QUẢN LÝ PHÒNG ========================
        private void LoadPhong()
        {
            // Lấy danh sách Phong từ EF và chuyển thành list anonymous có trường STT (index)
            var list = db.Phongs
                         .OrderBy(p => p.SoPhong)
                         .ToList()
                         .Select((p, idx) => new
                         {
                             STT = idx + 1,
                             p.SoPhong,
                             p.Tang,
                             p.LoaiPhong,
                             p.Gia,
                             p.TinhTrang,
                             SoGioThue = p.SoGioThue ?? 0,
                             p.NgayThuePhong,
                             p.NgayTraPhong
                         })
                         .ToList();

            dtgQLP.DataSource = list;
            // Chuyển tên cột tiếng việt k dấu thành có dấu
            if (dtgQLP.Columns.Contains("NgayThuePhong"))
                dtgQLP.Columns["NgayThuePhong"].Visible = false;
            if (dtgQLP.Columns.Contains("NgayTraPhong"))
                dtgQLP.Columns["NgayTraPhong"].Visible = false;
            if (dtgQLP.Columns.Contains("STT"))
                dtgQLP.Columns["STT"].HeaderText = "STT";
            if (dtgQLP.Columns.Contains("SoPhong"))
                dtgQLP.Columns["SoPhong"].HeaderText = "Số phòng";
            if (dtgQLP.Columns.Contains("LoaiPhong"))
                dtgQLP.Columns["LoaiPhong"].HeaderText = "Loại phòng";
            if (dtgQLP.Columns.Contains("Tang"))
                dtgQLP.Columns["Tang"].HeaderText = "Tầng";
            if (dtgQLP.Columns.Contains("SoGioThue"))
                dtgQLP.Columns["SoGioThue"].HeaderText = "Số giờ thuê";
            if (dtgQLP.Columns.Contains("Gia"))
            {
                dtgQLP.Columns["Gia"].HeaderText = "Giá (VNĐ)";
                dtgQLP.Columns["Gia"].DefaultCellStyle.Format = "N0";
            }
            if (dtgQLP.Columns.Contains("TinhTrang"))
                dtgQLP.Columns["TinhTrang"].HeaderText = "Tình trạng";
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));

                var sb = new StringBuilder();
                foreach (byte b in bytes)
                {
                    // Phải là "X2" (IN HOA) để khớp với giá trị đã lưu
                    sb.Append(b.ToString("X2"));
                }
                return sb.ToString();
            }
        }

        // Hàm tính giá: cơ bản cho phòng Don, nhân hệ số theo loại
        private decimal TinhGia(string loaiPhong, string thoiGian)
        {
            decimal giaCoBan;
            switch (thoiGian)
            {
                case "1":  // 0–1h
                    giaCoBan = 60000;
                    break;
                case "3":  // 1–3h
                    giaCoBan = 150000;
                    break;
                case "12": // 3–12h
                    giaCoBan = 400000;
                    break;
                case "24": // 1 ngày
                    giaCoBan = 700000;
                    break;
                default:
                    giaCoBan = 0;
                    break;
            }

            if (string.IsNullOrEmpty(loaiPhong)) return giaCoBan;

            // So sánh với giá trị KHÔNG DẤU lưu/truyền "Don", "Doi", "Bon"
            if (loaiPhong.Equals("Don", StringComparison.OrdinalIgnoreCase)) return giaCoBan;
            if (loaiPhong.Equals("Doi", StringComparison.OrdinalIgnoreCase)) return giaCoBan * 1.5m;
            if (loaiPhong.Equals("Bon", StringComparison.OrdinalIgnoreCase)) return giaCoBan * 3m;
            return giaCoBan;
        }

        // Helper chuyển hiển thị -> DB (ở đây cả hiển thị và DB đều không dấu, nhưng tạm giữ để dùng nếu cần)
        private string NormalizeLoaiPhong(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            // chấp nhận cả dạng có dấu và không dấu: map về không dấu
            if (s.Equals("Đơn", StringComparison.OrdinalIgnoreCase) || s.Equals("Don", StringComparison.OrdinalIgnoreCase)) return "Don";
            if (s.Equals("Đôi", StringComparison.OrdinalIgnoreCase) || s.Equals("Doi", StringComparison.OrdinalIgnoreCase)) return "Doi";
            if (s.Equals("Bốn", StringComparison.OrdinalIgnoreCase) || s.Equals("Bon", StringComparison.OrdinalIgnoreCase)) return "Bon";
            return s;
        }

        private void cbTgThue_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Nếu chưa có loại phòng thì không xử lý
            string loai = NormalizeLoaiPhong(cbLoaiPhong?.Text?.Trim() ?? "");
            if (string.IsNullOrEmpty(loai)) return;

            // Lấy số ngày (nếu text là số) — ưu tiên dùng txtNgay nếu user nhập
            int soNgay = 0;
            if (int.TryParse(txtNgay?.Text?.Trim(), out int tmpNgay)) soNgay = tmpNgay;

            // Nếu người dùng chọn 1 mục giờ khác 0 -> vô hiệu txtNgay (chỉ chọn giờ)
            string selected = cbTgThue?.Text ?? "0";
            if (int.TryParse(selected, out int selVal) && selVal > 0)
            {
                // disable/clear ngày
                if (txtNgay != null)
                {
                    txtNgay.Text = "";
                    txtNgay.Enabled = false;
                }
            }
            else
            {
                // selected = "0" -> cho phép nhập ngày
                if (txtNgay != null) txtNgay.Enabled = true;
            }

            // Tính giá theo 2 input
            var (gia, soGio) = CalculatePriceFromSelection(loai, selected, soNgay);

            txtGia.Text = gia > 0 ? gia.ToString("N0") : "";
        }

        // ======================== TAB 2 - TRẢ PHÒNG ========================
        // ======================== FORM LOAD ========================

        // THAY THẾ HÀM CŨ BẰNG HÀM NÀY:

        // Bước 1: Thay đổi chữ ký hàm, thêm (bool chiDangThue = false)
        // (false là giá trị mặc định, nghĩa là nếu chỉ gọi LoadDanhSachKhachHang() thì nó sẽ hiện tất cả)
        private void LoadDanhSachKhachHang(bool chiDangThue = false)
        {
            try
            {
                // Bước 2: Tạo một truy vấn cơ sở (AsQueryable)
                var query = db.KhachHangs.AsQueryable();

                // Bước 3: Áp dụng bộ lọc NẾU chiDangThue là true
                if (chiDangThue)
                {
                    // Khách đang thuê là khách CHƯA CÓ ngày trả (NgayTra == null)
                    query = query.Where(k => k.NgayTra == null);
                }
                // Nếu chiDangThue là false, nó sẽ bỏ qua 'if' và lấy tất cả khách hàng

                // Lấy danh sách khách hàng theo filter, sắp xếp mới nhất lên đầu
                var list = query.OrderByDescending(k => k.STT).ToList();

                // Gán nguồn dữ liệu cho DataGridView
                dtgCTKH.DataSource = list;

                // ----- TOÀN BỘ PHẦN ĐỊNH DẠNG CỘT BÊN DƯỚI ĐƯỢC GIỮ NGUYÊN -----

                // ----- BỔ SUNG: tạo cột STT hiển thị (unbound) để không can thiệp vào trường khoá của entity -----
                if (!dtgCTKH.Columns.Contains("STT_Display"))
                {
                    var col = new DataGridViewTextBoxColumn
                    {
                        Name = "STT_Display",
                        HeaderText = "STT",
                        ReadOnly = true,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        Width = 50
                    };
                    dtgCTKH.Columns.Insert(0, col);
                }

                // Ẩn cột bound "STT" (cột này là IDENTITY/key trong DB) nếu tồn tại để tránh trùng lặp
                if (dtgCTKH.Columns.Contains("STT"))
                    dtgCTKH.Columns["STT"].Visible = false;


                // ----- /BỔ SUNG -----

                if (dtgCTKH.Columns.Contains("GioiTinh"))
                    dtgCTKH.Columns["GioiTinh"].HeaderText = "Gioi Tinh";

                // Định dạng cột ngày và tiền
                if (dtgCTKH.Columns.Contains("NgayThue"))
                    dtgCTKH.Columns["NgayThue"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";

                if (dtgCTKH.Columns.Contains("NgayTra"))
                    dtgCTKH.Columns["NgayTra"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";

                if (dtgCTKH.Columns.Contains("NgaySinh"))
                    dtgCTKH.Columns["NgaySinh"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";

                if (dtgCTKH.Columns.Contains("GiaThue"))
                    dtgCTKH.Columns["GiaThue"].DefaultCellStyle.Format = "N0";

                //Chuyển tên cột tiếng việt k dấu thành có dấu
                if (dtgCTKH.Columns.Contains("NgayThue"))
                    dtgCTKH.Columns["NgayThue"].HeaderText = "Ngày thuê";
                if (dtgCTKH.Columns.Contains("NgayTra"))
                    dtgCTKH.Columns["NgayTra"].HeaderText = "Ngày trả";

                if (dtgCTKH.Columns.Contains("STT_Display"))
                    dtgCTKH.Columns["STT_Display"].HeaderText = "STT";

                if (dtgCTKH.Columns.Contains("TenKhachHang"))
                    dtgCTKH.Columns["TenKhachHang"].HeaderText = "Tên khách hàng";
                if (dtgCTKH.Columns.Contains("CCCD"))
                    dtgCTKH.Columns["CCCD"].HeaderText = "CCCD/CMND";
                if (dtgCTKH.Columns.Contains("QuocTich"))
                    dtgCTKH.Columns["QuocTich"].HeaderText = "Quốc tịch";
                if (dtgCTKH.Columns.Contains("SoPhong"))
                    dtgCTKH.Columns["SoPhong"].HeaderText = "Số phòng";
                if (dtgCTKH.Columns.Contains("LoaiPhong"))
                    dtgCTKH.Columns["LoaiPhong"].HeaderText = "Loại phòng";
                if (dtgCTKH.Columns.Contains("GioiTinh"))
                    dtgCTKH.Columns["GioiTinh"].HeaderText = "Giới tính";
                if (dtgCTKH.Columns.Contains("SDT"))
                    dtgCTKH.Columns["SDT"].HeaderText = "SĐT";
                if (dtgCTKH.Columns.Contains("GiaThue"))
                {
                    dtgCTKH.Columns["GiaThue"].HeaderText = "Giá thuê (VNĐ)";
                    dtgCTKH.Columns["GiaThue"].DefaultCellStyle.Format = "N0";
                }
                if (dtgCTKH.Columns.Contains("DiaChi"))
                    dtgCTKH.Columns["DiaChi"].HeaderText = "Địa chỉ";
                if (dtgCTKH.Columns.Contains("NgaySinh"))
                    dtgCTKH.Columns["NgaySinh"].HeaderText = "Ngày sinh";

                // Force refresh UI (đảm bảo hiển thị ngay thay đổi)
                dtgCTKH.Refresh();
                dtgCTKH.ClearSelection();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi tải danh sách khách hàng: " + ex.Message);
            }
        }

        private void dtgCTKH_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            // Chỉ xử lý cho cột STT_Display
            if (e.ColumnIndex == dtgCTKH.Columns["STT_Display"]?.Index && e.RowIndex >= 0)
            {
                // Gán STT bằng chỉ số hàng + 1
                e.Value = (e.RowIndex + 1).ToString();
                e.FormattingApplied = true;
            }
        }

        private void txtSoPhong_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            string soPhong = txtSoPhong.Text.Trim();
            if (string.IsNullOrEmpty(soPhong))
            {
                LoadPhong();
                e.SuppressKeyPress = true;
                return;
            }

            // 1. Tìm phòng
            var phong = db.Phongs.FirstOrDefault(p => p.SoPhong == soPhong);

            // 2. Clear controls thời gian trước khi gán
            if (cbTgThue != null) cbTgThue.SelectedIndex = -1;
            if (txtNgay != null) txtNgay.Clear();
            if (txtGia != null) txtGia.Clear();
            if (cbLoaiPhong != null) cbLoaiPhong.SelectedIndex = -1;
            if (cbTinhTrang != null) cbTinhTrang.SelectedIndex = -1;

            // Đảm bảo event handlers được tháo tạm nếu cần (để tránh trigger logic phụ)
            if (cbTgThue != null) cbTgThue.SelectedIndexChanged -= cbTgThue_SelectedIndexChanged;
            if (txtNgay != null) txtNgay.TextChanged -= txtNgay_TextChanged;


            if (phong != null)
            {
                // 3. Gán thông tin cơ bản
                cbLoaiPhong.Text = phong.LoaiPhong;
                cbTinhTrang.Text = phong.TinhTrang;

                int soGio = phong.SoGioThue ?? 0;

                // 4. LOGIC HIỂN THỊ SỐ NGÀY/GIỜ ĐÃ THUÊ
                if (soGio > 0 && soGio % 24 == 0)
                {
                    // Nếu là số ngày chẵn
                    int days = soGio / 24;
                    if (txtNgay != null)
                    {
                        txtNgay.Text = days.ToString();
                        txtNgay.Enabled = true;
                    }
                    if (cbTgThue != null)
                    {
                        // Chọn "0" và Disable ComboBox Giờ (đã được làm trong Form2_Load, nhưng cần lặp lại)
                        int idx = -1;
                        for (int i = 0; i < cbTgThue.Items.Count; i++)
                            if ((cbTgThue.Items[i]?.ToString() ?? "") == "0") { idx = i; break; }

                        if (idx >= 0) cbTgThue.SelectedIndex = idx;
                        cbTgThue.Enabled = false;
                    }
                }
                else if (soGio > 0)
                {
                    // Nếu là số giờ lẻ
                    if (cbTgThue != null)
                    {
                        // Cố gắng chọn item có sẵn
                        bool set = false;
                        string hourText = soGio.ToString();
                        for (int i = 0; i < cbTgThue.Items.Count; i++)
                        {
                            if ((cbTgThue.Items[i]?.ToString() ?? "") == hourText)
                            {
                                cbTgThue.SelectedIndex = i;
                                set = true;
                                break;
                            }
                        }
                        if (!set) cbTgThue.Text = hourText; // Nếu không có item, gán Text
                        cbTgThue.Enabled = true;
                    }
                    if (txtNgay != null)
                    {
                        txtNgay.Clear();
                        txtNgay.Enabled = false;
                    }
                }
                else
                {
                    // Chưa thuê / SoGioThue = 0
                    if (cbTgThue != null) { cbTgThue.SelectedIndex = -1; cbTgThue.Enabled = true; }
                    if (txtNgay != null) { txtNgay.Clear(); txtNgay.Enabled = true; }
                }

                // 5. Nếu có giá sẵn thì hiển thị (Dùng giá đã lưu, không cần tính lại)
                txtGia.Text = phong.Gia?.ToString("N0") ?? "";
            }
            else
            {
                MessageBox.Show("Khong tim thay phong!");
                cbLoaiPhong.SelectedIndex = -1;
                cbTinhTrang.SelectedIndex = -1;
                cbTgThue.SelectedIndex = -1;
                txtGia.Clear();
                if (txtNgay != null) txtNgay.Clear();
            }

            // Gắn lại event handlers
            if (cbTgThue != null) cbTgThue.SelectedIndexChanged += cbTgThue_SelectedIndexChanged;
            if (txtNgay != null) txtNgay.TextChanged += txtNgay_TextChanged;


            e.SuppressKeyPress = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            db.Dispose();
        }
        private void txtSoP_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                FillRoomInfoFromSoPhong();
            }
        }

        private void txtSoP_Leave(object sender, EventArgs e)
        {
            FillRoomInfoFromSoPhong();
        }

        private void FillRoomInfoFromSoPhong()
        {
            try
            {
                string soPhong = (txtSoP?.Text ?? "").Trim();
                if (string.IsNullOrEmpty(soPhong)) return;

                // reset các control thời gian/giá trước khi gán dữ liệu phòng mới
                ResetTimeControls();

                var phong = db.Phongs.FirstOrDefault(p => p.SoPhong.Equals(soPhong, StringComparison.OrdinalIgnoreCase));
                if (phong == null)
                {
                    MessageBox.Show($"Không tìm thấy phòng {soPhong} trong hệ thống.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Helper tìm control sâu
                Func<string, Control> FindCtrl = (name) =>
                {
                    var arr = this.Controls.Find(name, true);
                    return (arr != null && arr.Length > 0) ? arr[0] : null;
                };

                var cbLoai = FindCtrl("cbLoaiPhong") as ComboBox ?? FindCtrl("cbLoaiPhongDK") as ComboBox;
                if (cbLoai != null) cbLoai.Text = (phong.LoaiPhong ?? "").Trim();

                // Giá
                var tbGia = FindCtrl("txtGia") as TextBox ?? FindCtrl("txtGiaDK") as TextBox;
                if (tbGia != null) tbGia.Text = (phong.Gia.HasValue && phong.Gia.Value > 0) ? phong.Gia.Value.ToString("N0") : "";

                // Controls thời gian
                var cbTg = FindCtrl("cbTgThue") as ComboBox;
                var tbNgay = FindCtrl("txtNgay") as TextBox;
                var nudNgay = FindCtrl("nudSoNgay") as NumericUpDown;

                // LƯU Ý: tháo event handlers tạm nếu có để tránh handler nội bộ phá đổi khi set programmatically
                if (cbTg != null)
                    cbTg.SelectedIndexChanged -= cbTgThue_SelectedIndexChanged;
                if (tbNgay != null)
                    tbNgay.TextChanged -= txtNgay_TextChanged;
                if (nudNgay != null)
                {
                    // nếu bạn có handler cho nud, tháo tương tự (ví dụ nudSoNgay_ValueChanged)
                    var m = this.GetType().GetMethod("nudSoNgay_ValueChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (m != null) nudNgay.ValueChanged -= (EventHandler)m.CreateDelegate(typeof(EventHandler), this);
                }

                int soGio = phong.SoGioThue ?? 0;

                if (soGio <= 0)
                {
                    // để trống cả ngày và giờ (đã reset ở đầu)
                    if (cbTg != null) { cbTg.SelectedIndex = -1; cbTg.Enabled = true; }
                    if (tbNgay != null) tbNgay.Text = "";
                    if (nudNgay != null) nudNgay.Value = nudNgay.Minimum;
                }
                else if (soGio % 24 == 0)
                {
                    // Hiển thị theo ngày
                    int days = soGio / 24;
                    if (nudNgay != null)
                    {
                        var clipped = Math.Max((decimal)nudNgay.Minimum, Math.Min((decimal)nudNgay.Maximum, (decimal)days));
                        nudNgay.Value = clipped;
                    }
                    else if (tbNgay != null)
                    {
                        tbNgay.Text = days.ToString();
                    }

                    // set cbTgThue = "0" và disable cbTgThue
                    if (cbTg != null)
                    {
                        int idx = -1;
                        for (int i = 0; i < cbTg.Items.Count; i++)
                            if ((cbTg.Items[i]?.ToString() ?? "") == "0") { idx = i; break; }

                        if (idx >= 0) cbTg.SelectedIndex = idx;
                        else cbTg.SelectedIndex = -1;

                        cbTg.Enabled = false;
                    }
                }
                else
                {
                    // Hiển thị theo giờ. Cố gắng chọn item tương ứng; nếu không có, đặt Text (và optionally thêm item tạm)
                    string hourText = soGio.ToString();
                    bool set = false;
                    if (cbTg != null)
                    {
                        for (int i = 0; i < cbTg.Items.Count; i++)
                        {
                            if ((cbTg.Items[i]?.ToString() ?? "") == hourText)
                            {
                                cbTg.SelectedIndex = i;
                                set = true;
                                break;
                            }
                        }

                        if (!set)
                        {
                            // Nếu ComboBox không có item tương ứng (ví dụ 48), đặt Text trực tiếp để hiển thị
                            // và đảm bảo combo được enable để người dùng có thể thay đổi
                            cbTg.SelectedIndex = -1;
                            cbTg.Text = hourText;
                        }

                        cbTg.Enabled = true;
                    }

                    // đảm bảo txtNgay/nud rỗng
                    if (tbNgay != null) tbNgay.Text = "";
                    if (nudNgay != null) nudNgay.Value = nudNgay.Minimum;
                }

                // Gán ngày thuê/tra nếu có
                //var dtpThue = FindCtrl("dtpNgayThue") as DateTimePicker;
                //var dtpTra = FindCtrl("dtpNgayTra") as DateTimePicker;
                //if (dtpThue != null && phong.NgayThuePhong.HasValue) dtpThue.Value = phong.NgayThuePhong.Value;
                //if (dtpTra != null && phong.NgayTraPhong.HasValue) dtpTra.Value = phong.NgayTraPhong.Value;

                // Khôi phục event handlers
                if (cbTg != null)
                    cbTg.SelectedIndexChanged += cbTgThue_SelectedIndexChanged;
                if (tbNgay != null)
                    tbNgay.TextChanged += txtNgay_TextChanged;
                if (nudNgay != null)
                {
                    var m = this.GetType().GetMethod("nudSoNgay_ValueChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (m != null) nudNgay.ValueChanged += (EventHandler)m.CreateDelegate(typeof(EventHandler), this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lấy thông tin phòng: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ----------------- 1) Helper: reset controls thời gian/giá -----------------
        private void ResetTimeControls()
        {
            // tìm control sâu trong form (hỗ trợ cả txtNgay hoặc nudSoNgay)
            Func<string, Control> FindCtrl = (name) =>
            {
                var arr = this.Controls.Find(name, true);
                return (arr != null && arr.Length > 0) ? arr[0] : null;
            };

            var cbTg = FindCtrl("cbTgThue") as ComboBox;
            var tbNgay = FindCtrl("txtNgay") as TextBox;
            var nudNgay = FindCtrl("nudSoNgay") as NumericUpDown;
            var txtGiaCtrl = FindCtrl("txtGia") as TextBox;

            if (cbTg != null)
            {
                cbTg.SelectedIndex = -1;
                cbTg.Enabled = true; // cho phép chọn giờ mặc định
            }

            if (tbNgay != null) tbNgay.Text = "";
            if (nudNgay != null) nudNgay.Value = nudNgay.Minimum;

            if (txtGiaCtrl != null) txtGiaCtrl.Text = ""; // xóa giá hiển thị
        }


        private void btnThemKH_Click(object sender, EventArgs e)
        {
            try
            {
                string tenKH = txtTenKH.Text.Trim();
                string diaChi = rtbDiaChi.Text.Trim();
                string sdt = txtSDT.Text.Trim();
                string quocTich = txtQuocTich.Text.Trim();
                string cccd = txtCCCD.Text.Trim();
                string soPhong = txtSoP.Text.Trim();

                string gioiTinh = cbGioiTinh.SelectedItem != null ? cbGioiTinh.SelectedItem.ToString() : "";
                DateTime ngaySinh = dtpNgaySinh.Value;
                DateTime ngayThue = DateTime.Now;
                DateTime? ngayTra = null;

                // Tên khách hàng >= 3 ký tự
                tenKH = tenKH.Trim();
                if (string.IsNullOrWhiteSpace(tenKH) || tenKH.Length < 3)
                {
                    MessageBox.Show("Tên khách hàng phải có ít nhất 3 ký tự.", "Thiếu/Không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtTenKH?.Focus();
                    return;
                }

                // CCCD: normalize -> chỉ chữ số, >= 10 chữ số
                string cccdDigits = System.Text.RegularExpressions.Regex.Replace(cccd ?? "", @"\D", "");
                if (string.IsNullOrWhiteSpace(cccdDigits) || cccdDigits.Length < 10)
                {
                    MessageBox.Show("CCCD phải gồm ít nhất 10 chữ số và không được chứa chữ.", "Thiếu/Không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtCCCD?.Focus();
                    return;
                }

                // SDT: normalize -> chỉ chữ số, >= 10 chữ số
                string sdtDigits = System.Text.RegularExpressions.Regex.Replace(sdt ?? "", @"\D", "");
                if (string.IsNullOrWhiteSpace(sdtDigits) || sdtDigits.Length < 10)
                {
                    MessageBox.Show("Số điện thoại phải gồm ít nhất 10 chữ số và không được chứa chữ.", "Thiếu/Không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtSDT?.Focus();
                    return;
                }

                if (string.IsNullOrEmpty(tenKH) || string.IsNullOrEmpty(soPhong))
                {
                    MessageBox.Show("Vui lòng nhập tên khách hàng và số phòng.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var phong = db.Phongs.FirstOrDefault(p => p.SoPhong.Equals(soPhong, StringComparison.OrdinalIgnoreCase));
                if (phong == null)
                {
                    MessageBox.Show($"Không tìm thấy phòng {soPhong}.", "Lỗi dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // --- TÍNH GIÁ VÀ SỐ GIỜ THEO LỰA CHỌN (ưu tiên ngày nếu nhập txtNgay) ---
                int soNgay = 0;
                if (!string.IsNullOrWhiteSpace(txtNgay?.Text))
                {
                    int.TryParse(txtNgay.Text.Trim(), out soNgay);
                }
                else
                {
                    // hỗ trợ NumericUpDown nếu bạn dùng nudSoNgay
                    var nud = this.Controls.Find("nudSoNgay", true).FirstOrDefault() as NumericUpDown;
                    if (nud != null) soNgay = (int)nud.Value;
                }

                // Nếu cbTgThue = "0" nghĩa là không chọn giờ
                string tgText = cbTgThue?.Text ?? "0";

                // Tính giá & giờ (CalculatePriceFromSelection: trả về (gia, soGio))
                var (gia, soGio) = CalculatePriceFromSelection(phong.LoaiPhong, tgText, soNgay);

                // Tạo KhachHang và gán GiaThue = gia
                var kh = new KhachHang
                {
                    TenKhachHang = tenKH,
                    CCCD = string.IsNullOrEmpty(cccd) ? null : cccd,
                    QuocTich = string.IsNullOrEmpty(quocTich) ? null : quocTich,
                    SoPhong = soPhong,
                    LoaiPhong = phong.LoaiPhong,
                    GioiTinh = gioiTinh,
                    NgayThue = ngayThue,
                    NgayTra = ngayTra,
                    SDT = string.IsNullOrEmpty(sdt) ? null : sdt,
                    GiaThue = gia,
                    DiaChi = string.IsNullOrEmpty(diaChi) ? null : diaChi,
                    NgaySinh = ngaySinh
                };

                // --- CẬP NHẬT PHÒNG: đặt thành đã thuê, cập nhật giá và số giờ ---
                phong.Gia = gia;
                phong.SoGioThue = soGio > 0 ? (int?)soGio : null;
                phong.TinhTrang = "Da thue";
                if (phong.NgayThuePhong == null)
                    phong.NgayThuePhong = DateTime.Now;
                // xóa NgayTra nếu đang thuê
                phong.NgayTraPhong = null;

                // Lưu cả KhachHang và cập nhật phòng 1 lần
                db.KhachHangs.Add(kh);

                try
                {
                    db.SaveChanges();
                }
                catch (DbUpdateException dbuEx)
                {
                    var inner = dbuEx.InnerException?.InnerException;
                    MessageBox.Show("Lỗi khi lưu KhachHang:\n" + (inner?.Message ?? dbuEx.Message), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // rollback đối tượng được attach nếu cần
                    try { db.Entry(kh).State = System.Data.Entity.EntityState.Detached; } catch { }
                    return;
                }

                // Refresh UI
                LoadDanhSachKhachHang();
                LoadTraPhongGrid(txtSoPTra.Text.Trim(), txtTenKHTraP.Text.Trim());
                LoadPhong();

                // Clear form inputs
                txtTenKH.Clear();
                rtbDiaChi.Clear();
                txtSDT.Clear();
                txtQuocTich.Clear();
                txtCCCD.Clear();
                txtSoP.Clear();
                cbGioiTinh.SelectedIndex = -1;
                if (dtpNgaySinh != null) dtpNgaySinh.Value = DateTime.Now; // Đặt về ngày hiện tại (hoặc ngày mặc định bạn muốn)
                if (dtpNgayThue != null) dtpNgayThue.Value = DateTime.Now; // Đặt về ngày hiện tại

                ResetTimeControls();
                MessageBox.Show("Thêm khách hàng thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi thêm khách hàng: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void SelectChiTietKhachHangTabAndLoad(int stt)
        {
            try
            {
                // Tìm TabPage có chữ "Chi" hoặc "Chi tiết" trong Text
                TabPage found = null;
                foreach (TabPage tp in this.tcQLKS.TabPages)
                {
                    if (!string.IsNullOrEmpty(tp.Text) && tp.Text.IndexOf("Chi", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = tp;
                        break;
                    }
                }

                if (found != null)
                {
                    this.tcQLKS.SelectedTab = found;
                }
                else
                {
                    // fallback: chọn tab index 2 (thay đổi nếu bạn biết index của Chi Tiết KH)
                    if (this.tcQLKS.TabPages.Count > 2) this.tcQLKS.SelectedIndex = 2;
                }

                LoadChiTietKhachHang(stt);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi chuyển sang Chi tiết KH: " + ex.Message);
            }
        }

        // Mẫu hàm LoadChiTietKhachHang (bạn sửa tên control trong tab Chi tiết cho phù hợp)
        private void LoadChiTietKhachHang(int stt)
        {
            var kh = db.KhachHangs.FirstOrDefault(k => k.STT == stt);
            if (kh == null) return;

            // Ví dụ các control ở tab Chi tiết bạn có tên: txtCT_Ten, txtCT_CCCD, txtCT_SoPhong, ...
            if (this.Controls.ContainsKey("txtCT_Ten")) (this.Controls["txtCT_Ten"] as TextBox).Text = kh.TenKhachHang;
            if (this.Controls.ContainsKey("txtCT_CCCD")) (this.Controls["txtCT_CCCD"] as TextBox).Text = kh.CCCD;
            if (this.Controls.ContainsKey("txtCT_SoPhong")) (this.Controls["txtCT_SoPhong"] as TextBox).Text = kh.SoPhong;
            if (this.Controls.ContainsKey("txtCT_LoaiPhong")) (this.Controls["txtCT_LoaiPhong"] as TextBox).Text = kh.LoaiPhong;
            if (this.Controls.ContainsKey("txtCT_SDT")) (this.Controls["txtCT_SDT"] as TextBox).Text = kh.SDT;
            if (this.Controls.ContainsKey("txtCT_QuocTich")) (this.Controls["txtCT_QuocTich"] as TextBox).Text = kh.QuocTich;
            if (this.Controls.ContainsKey("txtCT_DiaChi")) (this.Controls["txtCT_DiaChi"] as TextBox).Text = kh.DiaChi;
            if (this.Controls.ContainsKey("dtpCT_NgaySinh")) (this.Controls["dtpCT_NgaySinh"] as DateTimePicker).Value = kh.NgaySinh ?? DateTime.Now;
            if (this.Controls.ContainsKey("dtpCT_NgayThue")) (this.Controls["dtpCT_NgayThue"] as DateTimePicker).Value = kh.NgayThue;
            if (this.Controls.ContainsKey("dtpCT_NgayTra") && kh.NgayTra.HasValue) (this.Controls["dtpCT_NgayTra"] as DateTimePicker).Value = kh.NgayTra.Value;
            if (this.Controls.ContainsKey("txtCT_Gia")) (this.Controls["txtCT_Gia"] as TextBox).Text = (kh.GiaThue.HasValue ? kh.GiaThue.Value.ToString("N0") : "");
        }


        // đặt trong class Form2 (trước các method)


        // Load dữ liệu có tình trạng đã thuê
        private void LoadTraPhongGrid(string soPhongFilter = null, string tenKhFilter = null)
        {
            try
            {
                var ds = (from p in db.Phongs
                          join k in db.KhachHangs on p.SoPhong equals k.SoPhong
                          // CHỈ LỌC THEO TÌNH TRẠNG PHÒNG "Da thue"
                          where p.TinhTrang == "Da thue"
                                // *** THÊM DÒNG NÀY: ***
                                // Chỉ lấy khách hàng CHƯA CÓ ngày trả (đang thuê thực sự)
                                && k.NgayTra == null
                                // ***********************
                                // Áp dụng các bộ lọc bổ sung từ textbox
                                && (string.IsNullOrEmpty(soPhongFilter) || p.SoPhong.Contains(soPhongFilter))
                                && (string.IsNullOrEmpty(tenKhFilter) || k.TenKhachHang.Contains(tenKhFilter))
                          // Lấy bản ghi KhachHang mới nhất cho mỗi phòng đang thuê
                          group k by p into g // Nhóm theo đối tượng Phong
                                              // Lấy khách hàng có ngày thuê gần nhất trong nhóm phòng đó
                          let latestKh = g.OrderByDescending(kh => kh.NgayThue).FirstOrDefault()
                          where latestKh != null // Đảm bảo có khách hàng liên quan
                          select new // Tạo đối tượng mới để hiển thị
                          {
                              KhachHangSTT = latestKh.STT, // ID ẩn của khách hàng để dùng khi trả phòng
                              g.Key.SoPhong, // Số phòng (từ key của group là đối tượng Phong)
                              g.Key.Tang,
                              g.Key.LoaiPhong,
                              Gia = g.Key.Gia, // Lấy giá TỔNG hiện tại của phòng (đã gồm DV)
                              g.Key.TinhTrang,
                              latestKh.TenKhachHang, // Tên khách hàng mới nhất
                              latestKh.CCCD,
                              latestKh.GioiTinh,
                              NgayThue = latestKh.NgayThue // Ngày thuê của khách hàng mới nhất
                          }).OrderBy(x => x.SoPhong) // Sắp xếp theo số phòng
                            .ToList() // Thực thi truy vấn LINQ to Entities
                          .Select((x, idx) => new // Tạo STT hiển thị và định dạng lại dữ liệu
                          {
                              STT = idx + 1, // STT hiển thị trên grid
                              x.KhachHangSTT, // Giữ lại ID thật của khách hàng
                              x.SoPhong,
                              x.Tang,
                              x.LoaiPhong,
                              Gia = (x.Gia ?? 0).ToString("N0"), // Định dạng giá tiền
                              x.TinhTrang,
                              x.TenKhachHang,
                              x.CCCD,
                              x.GioiTinh,
                              NgayThue = x.NgayThue.ToString("dd/MM/yyyy HH:mm") // Định dạng ngày thuê
                          }).ToList();

                if (dtgTraPhong != null)
                {
                    dtgTraPhong.DataSource = null; // Xóa nguồn cũ trước khi gán mới
                    dtgTraPhong.DataSource = ds;
                    dtgTraPhong.Refresh(); // Đảm bảo grid được vẽ lại

                    // --- Cấu hình hiển thị cột ---
                    if (dtgTraPhong.Columns.Contains("STT")) dtgTraPhong.Columns["STT"].HeaderText = "STT";
                    if (dtgTraPhong.Columns.Contains("SoPhong")) dtgTraPhong.Columns["SoPhong"].HeaderText = "Số phòng";
                    if (dtgTraPhong.Columns.Contains("Tang")) dtgTraPhong.Columns["Tang"].HeaderText = "Tầng";
                    if (dtgTraPhong.Columns.Contains("LoaiPhong")) dtgTraPhong.Columns["LoaiPhong"].HeaderText = "Loại phòng";
                    if (dtgTraPhong.Columns.Contains("Gia"))
                    {
                        dtgTraPhong.Columns["Gia"].HeaderText = "Tổng Giá (VNĐ)";
                        dtgTraPhong.Columns["Gia"].DefaultCellStyle.Format = "N0";
                    }
                    if (dtgTraPhong.Columns.Contains("TinhTrang")) dtgTraPhong.Columns["TinhTrang"].HeaderText = "Tình trạng";
                    if (dtgTraPhong.Columns.Contains("TenKhachHang")) dtgTraPhong.Columns["TenKhachHang"].HeaderText = "Tên khách hàng";
                    if (dtgTraPhong.Columns.Contains("CCCD")) dtgTraPhong.Columns["CCCD"].HeaderText = "CCCD";
                    if (dtgTraPhong.Columns.Contains("GioiTinh")) dtgTraPhong.Columns["GioiTinh"].HeaderText = "Giới tính";
                    if (dtgTraPhong.Columns.Contains("NgayThue"))
                    {
                        dtgTraPhong.Columns["NgayThue"].HeaderText = "Ngày thuê";
                    }

                    // Ẩn cột KhachHangSTT (ID thật của khách hàng)
                    if (dtgTraPhong.Columns.Contains("KhachHangSTT"))
                    {
                        dtgTraPhong.Columns["KhachHangSTT"].Visible = false;
                    }

                    // Tự động điều chỉnh độ rộng cột
                    dtgTraPhong.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh sách trả phòng: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"Lỗi LoadTraPhongGrid: {ex.ToString()}");
            }
        }

        private void Txt_FilterChanged(object sender, EventArgs e)
        {
            LoadTraPhongGrid(txtSoPTra.Text.Trim(), txtTenKHTraP.Text.Trim());
        }

        // thêm khi ấn thêm thì Tình trạng tự động đổi thành đã thuê và đẩy lên database
        private void btnThemSua_Click(object sender, EventArgs e)
        {
            string soPhong = (txtSoPhong?.Text ?? "").Trim();
            string loaiPhongRaw = (cbLoaiPhong?.Text ?? "").Trim();
            string loaiPhong = NormalizeLoaiPhong(loaiPhongRaw); // "Don" / "Doi" / "Bon"

            // Mặc định khi thêm/sửa ta đặt thành đã thuê (theo logic cũ)
            cbTinhTrang.Text = "Da thue";
            string tinhTrangDb = "Da thue";

            // Lấy giá trị giờ combo (có thể là "0" nếu không chọn giờ)
            string thoiGian = (cbTgThue?.Text ?? "").Trim();

            // Lấy số ngày (txtNgay có thể null nếu bạn dùng NumericUpDown thay thế)
            int soNgay = 0;
            if (!string.IsNullOrWhiteSpace(txtNgay?.Text))
                int.TryParse(txtNgay.Text.Trim(), out soNgay);
            else
            {
                // hỗ trợ NumericUpDown nếu dùng nudSoNgay
                if (this.Controls.Find("nudSoNgay", true).FirstOrDefault() is NumericUpDown nud)
                    soNgay = (int)nud.Value;
            }

            // VALIDATION: cần có SoPhong, LoaiPhong và phải chọn giờ hoặc số ngày (>0)
            if (string.IsNullOrEmpty(soPhong) || string.IsNullOrEmpty(loaiPhong))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin (Số phòng và Loại phòng).", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool hasHour = int.TryParse(thoiGian, out int hourVal) && hourVal > 0;
            bool hasDay = soNgay > 0;

            if (!hasHour && !hasDay)
            {
                MessageBox.Show("Vui lòng chọn thời gian thuê: nhập số ngày (txtNgay) hoặc chọn số giờ trong cbTgThue (khác 0).", "Thiếu thời gian thuê", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Tính giá và số giờ (ưu tiên ngày nếu soNgay > 0)
            var (gia, soGio) = CalculatePriceFromSelection(loaiPhong, cbTgThue?.Text ?? "0", soNgay);

            // Hiển thị giá tạm trên form
            if (txtGia != null)
                txtGia.Text = gia > 0 ? gia.ToString("N0") : "";

            try
            {
                // Tìm phòng (so sánh không phân biệt hoa thường)
                var phong = db.Phongs.FirstOrDefault(p => p.SoPhong.Equals(soPhong, StringComparison.OrdinalIgnoreCase));

                if (phong != null)
                {
                    // CẬP NHẬT
                    phong.LoaiPhong = loaiPhong;
                    phong.Gia = gia;
                    phong.TinhTrang = tinhTrangDb;

                    // Gán SoGioThue: nếu soGio > 0 gán giá trị, nếu 0 thì đặt null
                    phong.SoGioThue = soGio > 0 ? (int?)soGio : null;

                    // Nếu chuyển sang đã thuê và trước đó chưa có ngày thuê -> gán ngày thuê
                    if (tinhTrangDb.Equals("Da thue", StringComparison.OrdinalIgnoreCase) && phong.NgayThuePhong == null)
                    {
                        phong.NgayThuePhong = DateTime.Now;
                        phong.NgayTraPhong = null;
                    }

                    // Nếu chuyển sang trống -> ghi nhận ngày trả và xóa ngày thuê
                    if (tinhTrangDb.Equals("Chua thue", StringComparison.OrdinalIgnoreCase))
                    {
                        phong.NgayTraPhong = DateTime.Now;
                        phong.NgayThuePhong = null;
                        phong.SoGioThue = null;
                        phong.Gia = 0;
                    }

                    db.SaveChanges();
                    MessageBox.Show("Đã cập nhật thông tin phòng vào cơ sở dữ liệu.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // THÊM MỚI
                    var p = new DoAn.Phong
                    {
                        SoPhong = soPhong,
                        LoaiPhong = loaiPhong,
                        Gia = gia,
                        TinhTrang = tinhTrangDb,
                        SoGioThue = soGio > 0 ? (int?)soGio : null,
                        NgayThuePhong = (tinhTrangDb.Equals("Da thue", StringComparison.OrdinalIgnoreCase)) ? DateTime.Now : (DateTime?)null,
                        NgayTraPhong = (tinhTrangDb.Equals("Chua thue", StringComparison.OrdinalIgnoreCase)) ? DateTime.Now : (DateTime?)null
                    };

                    db.Phongs.Add(p);
                    db.SaveChanges();
                    MessageBox.Show("Đã thêm phòng mới vào cơ sở dữ liệu.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Làm mới UI
                LoadPhong();
                LoadTraPhongGrid();

                // Clear form và reset lựa chọn thời gian
                txtSoPhong.Clear();
                cbLoaiPhong.SelectedIndex = -1;
                cbTinhTrang.SelectedIndex = -1;

                // reset cbTgThue & txtNgay (nếu có)
                if (cbTgThue != null) { cbTgThue.SelectedIndex = -1; cbTgThue.Enabled = true; }
                if (txtNgay != null) txtNgay.Clear();
                var nudCtrl = this.Controls.Find("nudSoNgay", true).FirstOrDefault() as NumericUpDown;
                if (nudCtrl != null) nudCtrl.Value = nudCtrl.Minimum;

                txtGia.Clear();
            }
            catch (DbEntityValidationException dbEx)
            {
                var sb = new StringBuilder();
                foreach (var validationErrors in dbEx.EntityValidationErrors)
                {
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        sb.AppendLine($"Property: {validationError.PropertyName} Error: {validationError.ErrorMessage}");
                    }
                }
                MessageBox.Show("Validation error:\n" + sb.ToString(), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lưu dữ liệu:\n" + ex.ToString(), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        // LOAD FORM NEW
        public Form2()
        {
            InitializeComponent();
            // Gắn load form
            this.Load += Form2_Load;

            this.tcQLKS.DrawItem += new DrawItemEventHandler(this.tabControl1_DrawItem);
            this.tcQLNV.DrawItem += new DrawItemEventHandler(this.tcQLNV_DrawItem);
            // Gắn event cho nút (đảm bảo nếu Designer chưa gắn)
            this.btnThemSua.Click -= btnThemSua_Click;
            this.btnThemSua.Click += btnThemSua_Click;

            this.btnTraPhong.Click -= btnTraPhong_Click;
            this.btnTraPhong.Click += btnTraPhong_Click;

            // Gắn handler thay đổi combobox nếu cần
            if (cbTgThue != null)
            {
                cbTgThue.SelectedIndexChanged -= cbTgThue_SelectedIndexChanged;
                cbTgThue.SelectedIndexChanged += cbTgThue_SelectedIndexChanged;
            }

            this.btnThemKH.Click -= btnThemKH_Click;
            this.btnThemKH.Click += btnThemKH_Click;

            this.txtSoP.Leave -= txtSoP_Leave;
            this.txtSoP.Leave += txtSoP_Leave;

            this.txtSoP.KeyDown -= txtSoP_KeyDown;
            this.txtSoP.KeyDown += txtSoP_KeyDown;
            //txtTenKHTraP.TextChanged -= Txt_FilterChanged;
            //txtTenKHTraP.TextChanged += Txt_FilterChanged;

            //txtSoPTra.TextChanged -= Txt_FilterChanged;
            //txtSoPTra.TextChanged += Txt_FilterChanged;

            // Cho combobox loaiphong chỉ nhìn, không nhập
            cbLoaiPhong.DropDownStyle = ComboBoxStyle.DropDownList;
            cbLoaiPhong.Enabled = false;

            // Cho combobox loaiphong chỉ nhìn, không nhập
            cbTinhTrang.DropDownStyle = ComboBoxStyle.DropDownList;
            cbTinhTrang.Enabled = false;

            this.btnThemNV.Click -= btnThemNV_Click;
            this.btnThemNV.Click += btnThemNV_Click;
            // Gắn sự kiện tìm kiếm nhân viên

            this.btnXoaNV.Click -= btnXoaNV_Click;
            this.btnXoaNV.Click += btnXoaNV_Click;
            // Gắn sự kiện xóa nhân viên

            this.btnDangXuat.Click -= btnDangXuat_Click;
            this.btnDangXuat.Click += btnDangXuat_Click;
            // Gắn sự kiện đăng xuất
            if (dtgTraPhong != null)
            {
                dtgTraPhong.CellClick -= dtgTraPhong_CellClick;
                dtgTraPhong.CellClick += dtgTraPhong_CellClick;
                // dtgTraPhong.CellDoubleClick -= dtgTraPhong_CellClick;
                // dtgTraPhong.CellDoubleClick += dtgTraPhong_CellClick;
            }
            txtDoanhThu.ReadOnly = true;

            if (txtTenNV != null)
            {
                txtTenNV.KeyPress -= LettersOnly_KeyPress;
                txtTenNV.KeyPress += LettersOnly_KeyPress;
                txtTenNV.TextChanged -= LettersOnly_TextChanged;
                txtTenNV.TextChanged += LettersOnly_TextChanged;
            }
            if (txtSDTNV != null)
            {
                txtSDTNV.KeyPress -= DigitsOnly_KeyPress;
                txtSDTNV.KeyPress += DigitsOnly_KeyPress;
                txtSDTNV.TextChanged -= DigitsOnly_TextChanged;
                txtSDTNV.TextChanged += DigitsOnly_TextChanged;
            }

            // --- Khách hàng (tab Đăng ký) ---
            if (txtTenKH != null)
            {
                txtTenKH.KeyPress -= LettersOnly_KeyPress;
                txtTenKH.KeyPress += LettersOnly_KeyPress;
                txtTenKH.TextChanged -= LettersOnly_TextChanged;
                txtTenKH.TextChanged += LettersOnly_TextChanged;
            }
            if (txtSDT != null)
            {
                txtSDT.KeyPress -= DigitsOnly_KeyPress;
                txtSDT.KeyPress += DigitsOnly_KeyPress;
                txtSDT.TextChanged -= DigitsOnly_TextChanged;
                txtSDT.TextChanged += DigitsOnly_TextChanged;
            }
            if (txtQuocTich != null)
            {
                txtQuocTich.KeyPress -= LettersOnly_KeyPress;
                txtQuocTich.KeyPress += LettersOnly_KeyPress;
                txtQuocTich.TextChanged -= LettersOnly_TextChanged;
                txtQuocTich.TextChanged += LettersOnly_TextChanged;
            }
            if (txtCCCD != null)
            {
                txtCCCD.KeyPress -= DigitsOnly_KeyPress;
                txtCCCD.KeyPress += DigitsOnly_KeyPress;
                txtCCCD.TextChanged -= DigitsOnly_TextChanged;
                txtCCCD.TextChanged += DigitsOnly_TextChanged;
            }
            if (txtSoP != null)
            {
                txtSoP.KeyPress -= DigitsOnly_KeyPress;
                txtSoP.KeyPress += DigitsOnly_KeyPress;
                txtSoP.TextChanged -= DigitsOnly_TextChanged;
                txtSoP.TextChanged += DigitsOnly_TextChanged;
            }
            btnDinhKem1.Click += btnDinhKem_Click;
            btnDinhKem2.Click += btnDinhKem_Click;
            btnXoaFile1.Click += btnXoaFile_Click;
            btnXoaFile2.Click += btnXoaFile_Click;

            if (dtgCTKH != null)
            {
                dtgCTKH.CellFormatting -= dtgCTKH_CellFormatting;
                dtgCTKH.CellFormatting += dtgCTKH_CellFormatting;
            }
            printDocument1.PrintPage += printDocument1_PrintPage;
        }

        // Sự kiện để tự vẽ lại các tab của tcQLNV
        private void tcQLNV_DrawItem(object sender, DrawItemEventArgs e)
        {
            // Lấy TabControl và TabPage hiện tại
            TabControl currentTabControl = tcQLNV; // Sử dụng tcQLNV
            TabPage tabPage = currentTabControl.TabPages[e.Index];
            Graphics g = e.Graphics;
            Rectangle tabBounds = currentTabControl.GetTabRect(e.Index);

            // --- 1. Vẽ nền tab ---
            Color backgroundColor = (e.State == DrawItemState.Selected) ? tabSelectedColor : tabUnselectedColor;
            using (Brush brush = new SolidBrush(backgroundColor))
            {
                g.FillRectangle(brush, tabBounds);
            }

            // --- 2. Vẽ Icon (nếu có) ---
            // Sử dụng ImageList đã gán cho tcQLNV (ví dụ: tabControlImageListNV)
            ImageList imageList = currentTabControl.ImageList;
            if (imageList != null && tabPage.ImageIndex >= 0 && tabPage.ImageIndex < imageList.Images.Count)
            {
                Image tabIcon = imageList.Images[tabPage.ImageIndex];
                if (tabIcon != null)
                {
                    int iconX = tabBounds.X + 5;
                    int iconY = tabBounds.Y + (tabBounds.Height - tabIcon.Height) / 2;
                    g.DrawImage(tabIcon, iconX, iconY);
                }
            }

            // --- 3. Vẽ Tên tab ---
            string tabText = tabPage.Text;
            Color textColor = Color.White; // Giữ màu chữ trắng
            using (Brush textBrush = new SolidBrush(textColor))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Near, // Căn trái
                    LineAlignment = StringAlignment.Center // Căn giữa theo chiều cao
                };
                Font tabFont = currentTabControl.Font; // Dùng font của tcQLNV
                RectangleF textRect = new RectangleF(tabBounds.X, tabBounds.Y, tabBounds.Width, tabBounds.Height);

                // Nếu có icon, dịch chữ sang phải
                if (imageList != null && tabPage.ImageIndex >= 0)
                {
                    textRect.X += imageList.ImageSize.Width + 10; // Khoảng cách sau icon
                    textRect.Width -= (imageList.ImageSize.Width + 10);
                }
                else
                {
                    textRect.X += 5; // Padding trái nếu không có icon
                    textRect.Width -= 5;
                }

                g.DrawString(tabText, tabFont, textBrush, textRect, sf);
            }
        }
        private void tabControl1_DrawItem(object sender, DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            TabPage tabPage = tcQLKS.TabPages[e.Index];
            Rectangle tabBounds = tcQLKS.GetTabRect(e.Index);

            // --- 1. Vẽ nền tab (Sử dụng màu đã định nghĩa) ---
            Color backgroundColor = (e.State == DrawItemState.Selected) ? tabSelectedColor : tabUnselectedColor; // Tự động chọn màu đậm hơn khi selected
            using (Brush brush = new SolidBrush(backgroundColor))
            {
                g.FillRectangle(brush, tabBounds);
            }

            // --- 2. Vẽ Icon ---
            // (Giữ nguyên code vẽ icon)
            if (tcQLKS.ImageList != null && tabPage.ImageIndex >= 0)
            {
                Image tabIcon = tcQLKS.ImageList.Images[tabPage.ImageIndex];
                if (tabIcon != null)
                {
                    int iconX = tabBounds.X + 5;
                    int iconY = tabBounds.Y + (tabBounds.Height - tabIcon.Height) / 2;
                    g.DrawImage(tabIcon, iconX, iconY);
                }
            }


            // --- 3. Vẽ Tên tab ---
            string tabText = tabPage.Text;
            Color textColor = Color.White; // Giữ màu chữ trắng
            using (Brush textBrush = new SolidBrush(textColor))
            {
                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center
                };
                Font tabFont = tcQLKS.Font;
                RectangleF textRect = new RectangleF(tabBounds.X, tabBounds.Y, tabBounds.Width, tabBounds.Height);

                if (tcQLKS.ImageList != null && tabPage.ImageIndex >= 0)
                {
                    textRect.X += tcQLKS.ImageList.ImageSize.Width + 10;
                    textRect.Width -= (tcQLKS.ImageList.ImageSize.Width + 10);
                }
                else
                {
                    textRect.X += 5;
                    textRect.Width -= 5;
                }

                g.DrawString(tabText, tabFont, textBrush, textRect, sf);
            }
        }

        // -------- Letters only: chặn gõ số + lọc paste (chỉ cho phép chữ, space, '-' và apostrophe) --------
        private void LettersOnly_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsLetter(e.KeyChar) && !char.IsWhiteSpace(e.KeyChar)
                && e.KeyChar != '\'' && e.KeyChar != '-')
            {
                e.Handled = true;
            }
        }

        private void LettersOnly_TextChanged(object sender, EventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            int selStart = tb.SelectionStart;
            int beforeLen = tb.Text?.Length ?? 0;

            // Loại bỏ chữ số nếu user paste (giữ letters, whitespace, '-', ''')
            string cleaned = System.Text.RegularExpressions.Regex.Replace(tb.Text ?? "", @"\d", "");

            if (!string.Equals(tb.Text, cleaned, StringComparison.Ordinal))
            {
                tb.Text = cleaned;
                int removed = beforeLen - cleaned.Length;
                int newPos = Math.Max(0, selStart - Math.Max(0, removed));
                tb.SelectionStart = Math.Min(cleaned.Length, newPos);
                tb.SelectionLength = 0;
            }
        }

        // -------- Digits only: chặn gõ chữ + lọc paste (chỉ giữ chữ số) --------
        private void DigitsOnly_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void DigitsOnly_TextChanged(object sender, EventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            int selStart = tb.SelectionStart;
            int beforeLen = tb.Text?.Length ?? 0;

            string digitsOnly = System.Text.RegularExpressions.Regex.Replace(tb.Text ?? "", @"\D", "");

            if (!string.Equals(tb.Text, digitsOnly, StringComparison.Ordinal))
            {
                tb.Text = digitsOnly;
                int removed = beforeLen - digitsOnly.Length;
                int newPos = Math.Max(0, selStart - Math.Max(0, removed));
                tb.SelectionStart = Math.Min(digitsOnly.Length, newPos);
                tb.SelectionLength = 0;
            }
        }



        //======================== Tab 3 - Chi tiết khách hàng ========================
        private void dtgCTKH_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return; // header
            var row = dtgCTKH.Rows[e.RowIndex].DataBoundItem as KhachHang;
            if (row == null) return;
            // Chuyển sang tab Chi tiết và load chi tiết
            SelectChiTietKhachHangTabAndLoad(row.STT);
        }

        private void txtTimKiemKH_TextChanged(object sender, EventArgs e)
        {
            string keyword = txtTimKiemKH.Text.Trim().ToLower();
            bool chiDangThue = chkDangThue.Checked; // Lấy trạng thái checkbox

            var query = db.KhachHangs.AsQueryable();

            // 1. Lọc theo trạng thái thuê (từ checkbox)
            if (chiDangThue)
            {
                query = query.Where(k => k.NgayTra == null);
            }

            // 2. Lọc theo từ khóa (từ textbox)
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(k => k.TenKhachHang != null &&
                                        k.TenKhachHang.ToLower().Contains(keyword));
            }

            // 3. Sắp xếp và lấy dữ liệu
            var khList = query.OrderByDescending(k => k.STT).ToList();

            // Hiển thị kết quả lên DataGridView
            dtgCTKH.DataSource = khList;
            dtgCTKH.Refresh(); // Đảm bảo grid cập nhật
        }

        private void txtTKNV_TextChanged(object sender, EventArgs e)
        {
            // Lấy từ khóa tìm kiếm từ TextBox
            string keyword = txtTKNV.Text; // Không cần Trim() hoặc ToLower() ở đây nếu LoadNhanVien đã xử lý

            // Gọi hàm LoadNhanVien với từ khóa để tải lại dữ liệu đã lọc và sắp xếp đúng
            LoadNhanVien(keyword);
        }

        //done
        //======================== Tab 4 - Quản lý nhân viên ========================
        //========================  
        // Load dữ liệu nhân viên
        private void LoadNhanVien(string keyword = null)
        {
            if (dtgTTNV == null) return;

            try // Thêm try-catch để bắt lỗi truy vấn hoặc gán dữ liệu
            {
                var q = db.NhanViens.AsQueryable();

                // Áp dụng bộ lọc tìm kiếm nếu có keyword (sử dụng ToLower() để tìm không phân biệt hoa/thường)
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    string kLower = keyword.Trim().ToLower();
                    q = q.Where(n =>
                        (n.MaNhanVien != null && n.MaNhanVien.ToLower().Contains(kLower)) ||
                        (n.TenNhanVien != null && n.TenNhanVien.ToLower().Contains(kLower)) ||
                        (n.SDT != null && n.SDT.Contains(kLower))); // Giả sử SDT không cần ToLower
                }

                // Sắp xếp (luôn tăng dần theo MaNV) và tạo đối tượng mới với STT hiển thị
                var list = q.OrderBy(n => n.MaNhanVien) // Luôn sắp xếp tăng dần
                            .ToList() // Thực thi truy vấn CSDL
                            .Select((n, idx) => new // Tạo đối tượng mới để hiển thị trên grid
                            {
                                STT_Display = idx + 1, // Tạo STT dựa trên index sau khi sắp xếp
                                n.MaNhanVien,
                                n.TenNhanVien,
                                n.GioiTinh,
                                n.SDT,
                                n.Email,
                                n.ChucVu,
                                n.CaLamViec,
                                NgaySinh = n.NgaySinh, // Giữ nguyên kiểu DateTime?
                                n.DiaChi,
                                n.TaiKhoan,
                                // Bỏ cột n.STT (STT gốc từ DB) nếu bạn không cần dùng đến nó nữa
                                // n.STT
                            })
                            .ToList();

                // Gán dữ liệu vào DataGridView
                dtgTTNV.DataSource = null; // Xóa nguồn cũ trước
                dtgTTNV.DataSource = list;

                // --- Cấu hình lại cột sau khi gán DataSource ---

                // Đảm bảo cột STT hiển thị liên kết đúng DataPropertyName
                if (dtgTTNV.Columns.Contains("STT")) // Giả sử cột hiển thị tên là "STT"
                {
                    dtgTTNV.Columns["STT"].HeaderText = "STT";
                    // *** QUAN TRỌNG: Gán DataPropertyName cho cột STT hiển thị ***
                    dtgTTNV.Columns["STT"].DataPropertyName = "STT_Display";
                }

                // Cấu hình HeaderText và DataPropertyName cho các cột khác (để đảm bảo khớp)
                if (dtgTTNV.Columns.Contains("MaNhanVien"))
                {
                    dtgTTNV.Columns["MaNhanVien"].HeaderText = "Mã nhân viên";
                    dtgTTNV.Columns["MaNhanVien"].DataPropertyName = "MaNhanVien";
                }
                if (dtgTTNV.Columns.Contains("TenNhanVien"))
                {
                    dtgTTNV.Columns["TenNhanVien"].HeaderText = "Tên nhân viên";
                    dtgTTNV.Columns["TenNhanVien"].DataPropertyName = "TenNhanVien";
                }
                if (dtgTTNV.Columns.Contains("GioiTinh"))
                {
                    dtgTTNV.Columns["GioiTinh"].HeaderText = "Giới tính";
                    dtgTTNV.Columns["GioiTinh"].DataPropertyName = "GioiTinh";
                }
                if (dtgTTNV.Columns.Contains("SDT"))
                {
                    dtgTTNV.Columns["SDT"].HeaderText = "Số điện thoại";
                    dtgTTNV.Columns["SDT"].DataPropertyName = "SDT";
                }
                if (dtgTTNV.Columns.Contains("Email"))
                {
                    dtgTTNV.Columns["Email"].HeaderText = "Email";
                    dtgTTNV.Columns["Email"].DataPropertyName = "Email";
                }
                if (dtgTTNV.Columns.Contains("ChucVu"))
                {
                    dtgTTNV.Columns["ChucVu"].HeaderText = "Chức vụ";
                    dtgTTNV.Columns["ChucVu"].DataPropertyName = "ChucVu";
                }
                if (dtgTTNV.Columns.Contains("CaLamViec"))
                {
                    dtgTTNV.Columns["CaLamViec"].HeaderText = "Ca làm việc";
                    dtgTTNV.Columns["CaLamViec"].DataPropertyName = "CaLamViec";
                }
                if (dtgTTNV.Columns.Contains("NgaySinh"))
                {
                    dtgTTNV.Columns["NgaySinh"].HeaderText = "Ngày sinh";
                    dtgTTNV.Columns["NgaySinh"].DataPropertyName = "NgaySinh";
                    // Có thể cần định dạng lại cột ngày sinh nếu cần
                    // dtgTTNV.Columns["NgaySinh"].DefaultCellStyle.Format = "dd/MM/yyyy";
                }
                if (dtgTTNV.Columns.Contains("DiaChi"))
                {
                    dtgTTNV.Columns["DiaChi"].HeaderText = "Địa chỉ";
                    dtgTTNV.Columns["DiaChi"].DataPropertyName = "DiaChi";
                }
                if (dtgTTNV.Columns.Contains("TaiKhoan"))
                {
                    dtgTTNV.Columns["TaiKhoan"].HeaderText = "Tài khoản";
                    dtgTTNV.Columns["TaiKhoan"].DataPropertyName = "TaiKhoan";
                }

                // Ẩn cột mật khẩu (giữ nguyên)
                if (dtgTTNV.Columns.Contains("MatKhau"))
                    dtgTTNV.Columns["MatKhau"].Visible = false;

                // Ẩn cột STT gốc từ DB nếu nó vẫn còn (và không phải là cột STT hiển thị)
                if (dtgTTNV.Columns.Contains("STT_DB_Original")) // Giả sử bạn có cột STT gốc tên này
                    dtgTTNV.Columns["STT_DB_Original"].Visible = false;

                dtgTTNV.Refresh(); // Đảm bảo grid cập nhật hiển thị
                dtgTTNV.ClearSelection(); // Bỏ chọn dòng
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải/tìm kiếm nhân viên: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeNhanVienControls()
        {
            // Populate cbChucVu
            if (cbChucVu != null)
            {
                cbChucVu.Items.Clear();
                cbChucVu.Items.Add("Quản Lý");
                cbChucVu.Items.Add("Lễ tân");
                cbChucVu.DropDownStyle = ComboBoxStyle.DropDownList;
                cbChucVu.SelectedIndex = -1;
            }

            // Populate cbGTNV (giới tính)
            if (cbGTNV != null)
            {
                cbGTNV.Items.Clear();
                cbGTNV.Items.Add("Nam");
                cbGTNV.Items.Add("Nữ");
                cbGTNV.Items.Add("Khác");
                cbGTNV.DropDownStyle = ComboBoxStyle.DropDownList;
                cbGTNV.SelectedIndex = -1;
            }

            // Populate cbCa (ca làm việc)
            if (cbCa != null)
            {
                cbCa.Items.Clear();
                cbCa.Items.Add("Sáng");
                cbCa.Items.Add("Chiều");
                cbCa.Items.Add("Cả ngày");
                cbCa.DropDownStyle = ComboBoxStyle.DropDownList;
                cbCa.SelectedIndex = -1;
            }

            // DateTimePicker cho ngày sinh: định dạng dd/MM/yyyy HH:mm
            if (dtpNSNV != null)
            {
                dtpNSNV.Format = DateTimePickerFormat.Custom;
                dtpNSNV.CustomFormat = "dd/MM/yyyy";
                dtpNSNV.ShowUpDown = true; // hiển thị spinner thay vì calendar
                dtpNSNV.Value = DateTime.Now;
            }
        }

        // Helper: nạp template HTML, tạo template mặc định nếu chưa có, và thay placeholder
        private string LoadAndPopulateTemplate(string templateFileName, Dictionary<string, string> values)
        {
            try
            {
                string folder = Path.Combine(Application.StartupPath, "EmailTemplates");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, templateFileName);

                string template = File.ReadAllText(path, Encoding.UTF8);

                // Thay placeholder: {{Key}}
                if (values != null)
                {
                    foreach (var kv in values)
                    {
                        // bảo đảm escape HTML nội dung thay thế
                        string safe = kv.Value == null ? "" : System.Net.WebUtility.HtmlEncode(kv.Value);
                        template = template.Replace("{{" + kv.Key + "}}", safe);
                    }
                }

                return template;
            }
            catch
            {
                // fallback đơn giản nếu có lỗi
                return "";
            }
        }

        // Gắn vào nút Thêm nhân viên (btnThemNV)
        private async void btnThemNV_Click(object sender, EventArgs e)
        {
            // Lấy thông tin người dùng đang đăng nhập (từ Form1 qua Tag)
            var user = this.Tag as NhanVien;

            // 1. KIỂM TRA QUYỀN
            if (user?.ChucVu != "Quan ly")
            {
                MessageBox.Show("Bạn không có quyền thêm nhân viên!", "Không có quyền", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            try
            {
                // 2. Đọc và Validate giá trị từ controls
                string MaNhanVien = txtMaNV?.Text.Trim() ?? "";
                string tenNV = txtTenNV?.Text.Trim() ?? "";
                string email = txtEmail?.Text.Trim() ?? "";
                string sdt = txtSDTNV?.Text.Trim() ?? "";
                string chucVu = cbChucVu?.Text.Trim() ?? "";
                string gioiTinh = cbGTNV?.Text.Trim() ?? "";
                DateTime ngaySinh = dtpNSNV != null ? dtpNSNV.Value : DateTime.Now;
                string diaChi = rtbDCNV?.Text.Trim() ?? "";
                string caLamViec = cbCa?.Text.Trim() ?? "";
                string taiKhoan = txtThemTK?.Text.Trim() ?? "";
                string matKhauPlainText = txtThemMK?.Text ?? ""; // Giữ tên này để dễ phân biệt

                if (string.IsNullOrWhiteSpace(MaNhanVien) || string.IsNullOrWhiteSpace(tenNV) || string.IsNullOrWhiteSpace(taiKhoan) || string.IsNullOrWhiteSpace(matKhauPlainText))
                {
                    MessageBox.Show("Vui lòng nhập đầy đủ Mã NV, Họ tên, Tài khoản và Mật khẩu.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    (string.IsNullOrWhiteSpace(MaNhanVien) ? txtMaNV : txtThemMK)?.Focus();
                    return;
                }

                // 3. Chuẩn hóa giá trị (Đã giữ logic của bạn)
                if (chucVu.Equals("Quản lý", StringComparison.OrdinalIgnoreCase)) chucVu = "Quan ly";
                else if (chucVu.Equals("Lễ tân", StringComparison.OrdinalIgnoreCase)) chucVu = "Le tan";

                if (caLamViec.Equals("Sáng", StringComparison.OrdinalIgnoreCase)) caLamViec = "Sang";
                else if (caLamViec.Equals("Chiều", StringComparison.OrdinalIgnoreCase)) caLamViec = "Chieu";
                else if (caLamViec.Equals("Cả ngày", StringComparison.OrdinalIgnoreCase)) caLamViec = "Ca ngay";

                // 4. KIỂM TRA TRÙNG LẶP
                if (db.NhanViens.Any(n => n.MaNhanVien == MaNhanVien))
                {
                    MessageBox.Show($"Mã nhân viên '{MaNhanVien}' đã tồn tại.", "Trùng mã", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtMaNV?.Focus();
                    return;
                }

                if (db.NhanViens.Any(n => n.TaiKhoan == taiKhoan))
                {
                    MessageBox.Show($"Tài khoản '{taiKhoan}' đã tồn tại.", "Trùng tài khoản", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtThemTK?.Focus();
                    return;
                }

                // 5. ✨ MÃ HÓA MẬT KHẨU ✨
                string matKhauDaBam = HashPassword(matKhauPlainText);

                // 6. Tạo entity và lưu
                var nv = new NhanVien
                {
                    MaNhanVien = MaNhanVien,
                    TenNhanVien = tenNV,
                    Email = string.IsNullOrWhiteSpace(email) ? null : email,
                    SDT = string.IsNullOrWhiteSpace(sdt) ? null : sdt,
                    ChucVu = string.IsNullOrWhiteSpace(chucVu) ? null : chucVu,
                    GioiTinh = string.IsNullOrWhiteSpace(gioiTinh) ? null : gioiTinh,
                    NgaySinh = ngaySinh,
                    DiaChi = string.IsNullOrWhiteSpace(diaChi) ? null : diaChi,
                    CaLamViec = string.IsNullOrWhiteSpace(caLamViec) ? null : caLamViec,
                    TaiKhoan = taiKhoan, // Đã kiểm tra is not null
                    MatKhau = matKhauDaBam // GÁN MẬT KHẨU ĐÃ BĂM
                };

                db.NhanViens.Add(nv);

                // --- Bắt lỗi DbUpdateException ---
                try
                {
                    db.SaveChanges();
                }
                catch (DbUpdateException dbuEx)
                {
                    var inner = dbuEx.InnerException?.InnerException?.Message ?? dbuEx.Message;
                    MessageBox.Show("Lỗi khi lưu nhân viên (DB):\n" + inner, "Lỗi Database", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    // Tách entity ra khỏi context để tránh lỗi khi cố gắng lưu lại
                    try { db.Entry(nv).State = System.Data.Entity.EntityState.Detached; } catch { }
                    return;
                }
                // ---------------------------------

                // 7. Gửi Email (Sử dụng Task.Run để không làm chậm giao diện)
                try
                {
                    if (!string.IsNullOrWhiteSpace(nv.Email))
                    {
                        var placeholders = new Dictionary<string, string>
                        {
                            ["TenNhanVien"] = nv.TenNhanVien,
                            ["MaNhanVien"] = nv.MaNhanVien,
                            ["ChucVu"] = nv.ChucVu ?? "",
                            ["TaiKhoan"] = nv.TaiKhoan ?? ""
                        };

                        // Giả định các hàm LoadAndPopulateTemplate, attachmentPaths, EmailService đã được định nghĩa
                        string body = LoadAndPopulateTemplate("EmployeeAdded.html", placeholders);
                        var attach = attachmentPaths.ContainsKey("tab1") && !string.IsNullOrWhiteSpace(attachmentPaths["tab1"])
                        ? new[] { attachmentPaths["tab1"] }
                        : null;

                        Task.Run(async () =>
                        {
                            try
                            {
                                var err = await EmailService.SendEmailAsync(nv.Email, $"Thông báo: Tài khoản nhân viên {nv.MaNhanVien}", body, attach);
                                if (err != null) File.AppendAllText("email_errors.log", $"{DateTime.Now}: {err}\n");
                            }
                            catch (Exception ex)
                            {
                                File.AppendAllText("email_errors.log", $"{DateTime.Now}: Exception {ex}\n");
                            }
                        });
                    }

                }
                catch (Exception exSend)
                {
                    MessageBox.Show("Lưu nhân viên thành công nhưng lỗi gửi email: " + exSend.Message, "Lỗi gửi mail", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }


                // 8. HOÀN THÀNH VÀ CẬP NHẬT GIAO DIỆN
                LoadNhanVien(); // Cập nhật DataGrid

                // Reset form inputs (đã làm rất tốt)
                txtMaNV?.Clear();
                txtTenNV?.Clear();
                txtEmail?.Clear();
                txtSDTNV?.Clear();
                cbChucVu.SelectedIndex = -1;
                cbGTNV.SelectedIndex = -1;
                cbCa.SelectedIndex = -1;
                dtpNSNV.Value = DateTime.Now;
                rtbDCNV.Clear();
                txtThemTK.Clear();
                txtThemMK.Clear();
                ResetFileAttachments(); // Giả định hàm này tồn tại

                MessageBox.Show("Thêm nhân viên thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                // Bắt các lỗi chung khác (ví dụ: lỗi tham chiếu null)
                MessageBox.Show("Lỗi nghiêm trọng khi thêm nhân viên: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnXoaNV_Click(object sender, EventArgs e)
        {
            var user = this.Tag as NhanVien; // Lấy thông tin người dùng đang đăng nhập
                                             // 1. Kiểm tra quyền cơ bản: Chỉ quản lý mới được xóa
            if (user?.ChucVu != "Quan ly")
            {
                MessageBox.Show("Bạn không có quyền xóa nhân viên!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // 2. Kiểm tra xem có dòng nào được chọn không
                if (dtgTTNV == null || dtgTTNV.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất 1 nhân viên để xóa.", "Chú ý", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 3. Lấy danh sách Mã NV và các đối tượng NhanVien tương ứng được chọn để xóa
                var maNVs = dtgTTNV.SelectedRows
                    .Cast<DataGridViewRow>()
                    .Where(r => r.Cells["MaNhanVien"].Value != null)
                    .Select(r => r.Cells["MaNhanVien"].Value.ToString())
                    .ToList();

                // Lấy danh sách các đối tượng NhanVien đầy đủ từ DB dựa trên mã đã chọn
                var employeesToDelete = db.NhanViens.Where(n => maNVs.Contains(n.MaNhanVien)).ToList();

                // Đếm xem có bao nhiêu quản lý ('Quan ly') trong danh sách sắp bị xóa
                int managersSelectedForDeletionCount = employeesToDelete.Count(nv => nv.ChucVu == "Quan ly");

                // Nếu người dùng đang cố gắng xóa ít nhất một quản lý...
                if (managersSelectedForDeletionCount > 0)
                {
                    // ...thì đếm tổng số quản lý hiện có trong toàn bộ database
                    int totalManagersInSystemCount = db.NhanViens.Count(n => n.ChucVu == "Quan ly");

                    // Nếu số quản lý sắp xóa BẰNG hoặc LỚN HƠN tổng số quản lý hiện có...
                    if (managersSelectedForDeletionCount >= totalManagersInSystemCount)
                    {
                        // ...thì báo lỗi và dừng lại
                        MessageBox.Show("Không thể xóa quản lý cuối cùng. Hệ thống phải còn lại ít nhất một quản lý.",
                                        "Thao tác bị chặn", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return; // Ngăn chặn việc xóa
                    }
                }


                // 5. Xác nhận lại với người dùng trước khi xóa
                var confirm = MessageBox.Show(
                    // Sử dụng employeesToDelete.Count thay vì maNVs.Count để đảm bảo đếm đúng số đối tượng tìm thấy
                    $"Bạn có chắc muốn xóa {employeesToDelete.Count} nhân viên đã chọn không?",
                    "Xác nhận xóa nhiều nhân viên",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                // Nếu người dùng không đồng ý, dừng lại
                if (confirm != DialogResult.Yes) return;

                int actualDeletedCount = 0; // Đếm số lượng thực tế bị xóa
                var employeesActuallyDeleted = new List<NhanVien>(); // List chứa những NV thực sự sẽ bị xóa (sau khi check tự xóa)

                // 6. Lặp qua danh sách nhân viên sẽ xóa để kiểm tra tự xóa và gửi email
                foreach (var nv in employeesToDelete) // Duyệt qua danh sách đối tượng đầy đủ
                {
                    // Kiểm tra nếu người dùng đang cố xóa chính mình
                    if (user.MaNhanVien == nv.MaNhanVien)
                    {
                        var selfConfirm = MessageBox.Show(
                            "Bạn đang xóa chính tài khoản của mình. Sau khi xóa, bạn sẽ bị đăng xuất khỏi hệ thống.\nTiếp tục?",
                            "Cảnh báo",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning
                        );
                        // Nếu không đồng ý tự xóa, bỏ qua nhân viên này và tiếp tục vòng lặp
                        if (selfConfirm != DialogResult.Yes)
                        {
                            continue;
                        }
                    }

                    // Nếu không phải tự xóa hoặc đã đồng ý tự xóa, thêm vào danh sách sẽ xóa thực tế
                    employeesActuallyDeleted.Add(nv);
                    actualDeletedCount++;

                    // Gửi email thông báo (fire-and-forget) - Logic gửi email giữ nguyên
                    if (!string.IsNullOrWhiteSpace(nv.Email))
                    {
                        var placeholders = new Dictionary<string, string>
                        {
                            ["TenNhanVien"] = nv.TenNhanVien,
                            ["MaNhanVien"] = nv.MaNhanVien,
                            ["ChucVu"] = nv.ChucVu ?? ""
                        };
                        string body = LoadAndPopulateTemplate("EmployeeDeleted.html", placeholders);
                        string subject = $"[Hệ thống] Tài khoản {nv.MaNhanVien} ({nv.TenNhanVien}) đã bị xóa";
                        var attach = attachmentPaths.ContainsKey("tab2") && !string.IsNullOrWhiteSpace(attachmentPaths["tab2"])
                                     ? new[] { attachmentPaths["tab2"] }
                                     : null;

                        // Chạy gửi email bất đồng bộ để không chặn giao diện
                        Task.Run(async () =>
                        {
                            try
                            {
                                var err = await EmailService.SendEmailAsync(nv.Email, subject, body, attach);
                                if (err != null)
                                    File.AppendAllText("email_errors.log", $"{DateTime.Now}: {nv.Email} - {err}\n");
                            }
                            catch (Exception ex)
                            {
                                File.AppendAllText("email_errors.log", $"{DateTime.Now}: Exception {ex}\n");
                            }
                        });
                    }
                } // Kết thúc vòng lặp foreach

                // 7. Thực hiện xóa khỏi database NẾU có nhân viên trong danh sách xóa thực tế
                if (employeesActuallyDeleted.Any())
                {
                    // Tối ưu hóa việc xóa nhiều bản ghi
                    bool prevAutoDetect = db.Configuration.AutoDetectChangesEnabled;
                    db.Configuration.AutoDetectChangesEnabled = false; // Tắt tự động dò thay đổi
                    try
                    {
                        // Xóa danh sách nhân viên đã được xác nhận xóa
                        db.NhanViens.RemoveRange(employeesActuallyDeleted);
                        db.SaveChanges(); // Lưu thay đổi vào database
                    }
                    finally
                    {
                        db.Configuration.AutoDetectChangesEnabled = prevAutoDetect; // Bật lại tự động dò thay đổi
                    }

                    // 8. Cập nhật giao diện và thông báo
                    LoadNhanVien(); // Tải lại danh sách nhân viên lên DataGridView

                    MessageBox.Show($"Đã xóa {actualDeletedCount} nhân viên.\n(Nếu có lỗi gửi email sẽ được ghi vào email_errors.log)",
                                    "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ResetFileAttachments(); // Reset phần đính kèm file (nếu có)

                    // 9. Xử lý trường hợp người dùng tự xóa tài khoản của mình
                    // Kiểm tra xem user hiện tại có trong danh sách đã bị xóa không
                    if (employeesActuallyDeleted.Any(nv => nv.MaNhanVien == user.MaNhanVien))
                    {
                        MessageBox.Show("Tài khoản của bạn đã bị xóa. Hệ thống sẽ quay về màn hình đăng nhập.",
                                        "Đăng xuất", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Close(); 
                    }
                }
                else // Nếu không có ai trong danh sách xóa thực tế (ví dụ hủy tự xóa)
                {
                    MessageBox.Show("Không có nhân viên nào được xóa.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex) // Bắt lỗi chung
            {
                MessageBox.Show("Lỗi khi xóa nhân viên: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDangXuat_Click(object sender, EventArgs e)
        {
            try
            {
                var confirm = MessageBox.Show("Bạn có chắc muốn đăng xuất và quay về màn hình đăng nhập?", "Đăng xuất", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes) return;

                // 1) Nếu Form2 được mở bởi Form1 và Owner đã được set -> show Owner
                if (this.Owner is Form1 ownerForm)
                {
                    if (!ownerForm.IsDisposed)
                    {
                        ownerForm.Show();
                    }
                    else
                    {
                        // Nếu owner đã disposed (hiếm khi xảy ra) tạo Form1 mới
                        var newLogin = new Form1();
                        newLogin.Show();
                    }
                }
                else
                {
                    // 2) Nếu Owner không tồn tại, thử tìm Form1 trong danh sách OpenForms
                    var existing = Application.OpenForms.OfType<Form1>().FirstOrDefault();
                    if (existing != null)
                    {
                        existing.Show();
                    }
                    else
                    {
                        // 3) Không tìm thấy -> tạo Form1 mới và show
                        var newLogin = new Form1();
                        newLogin.Show();
                    }
                }

                // Đóng Form2 (giải phóng tài nguyên)
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi đăng xuất: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Gắn vào class Form2 (bất kỳ chỗ nào trong class, ví dụ bên dưới các method hiện có)
        private void dtgTraPhong_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Nếu click header hoặc ngoài vùng dữ liệu -> bỏ qua
            if (e.RowIndex < 0) return;

            if (Control.ModifierKeys == Keys.Control || Control.ModifierKeys == Keys.Shift)
            {
                return;
            }

            try
            {
                var row = dtgTraPhong.Rows[e.RowIndex];

                // Thử lấy từ cell theo tên cột (nếu bạn đang tạo cột với tên "TenKhachHang" và "SoPhong")
                string ten = "";
                string so = "";

                if (dtgTraPhong.Columns.Contains("TenKhachHang"))
                    ten = row.Cells["TenKhachHang"].Value?.ToString() ?? "";

                if (dtgTraPhong.Columns.Contains("SoPhong"))
                    so = row.Cells["SoPhong"].Value?.ToString() ?? "";

                // Nếu không lấy được bằng cell (ví dụ anonymous type hoặc column khác tên), fallback sang DataBoundItem + reflection
                if (string.IsNullOrWhiteSpace(ten) || string.IsNullOrWhiteSpace(so))
                {
                    var data = row.DataBoundItem;
                    if (data != null)
                    {
                        var tProp = data.GetType().GetProperty("TenKhachHang");
                        var sProp = data.GetType().GetProperty("SoPhong");
                        if (tProp != null && string.IsNullOrWhiteSpace(ten))
                            ten = tProp.GetValue(data)?.ToString() ?? "";
                        if (sProp != null && string.IsNullOrWhiteSpace(so))
                            so = sProp.GetValue(data)?.ToString() ?? "";
                    }
                }

                if (dtpTraP != null) // Thay dtpTraP bằng tên thật
                {
                    dtpTraP.Value = DateTime.Now;
                    dtpTraP.Checked = true; // Đảm bảo nó được check (nếu dùng ShowCheckbox)
                }

                // Gán vào textbox (nếu các control tồn tại)
                if (this.Controls.ContainsKey("txtTenKHTraP"))
                    txtTenKHTraP.Text = ten;
                else
                    txtTenKHTraP.Text = ten; // hoặc trực tiếp nếu bạn dùng field

                if (this.Controls.ContainsKey("txtSoPTra"))
                    txtSoPTra.Text = so;
                else
                    txtSoPTra.Text = so;

                // Optionally select the row in UI
                dtgTraPhong.ClearSelection();
                row.Selected = true;
            }
            catch (Exception)
            {
                // bỏ qua lỗi nhỏ, hoặc bạn có thể log nếu cần
            }
        }


        private void Form2_Load(object sender, EventArgs e)
        {
            // Load dữ liệu lên grid
            LoadPhong();
            LoadTraPhongGrid();
            InitializeNhanVienControls();
            InitializeServiceTab();
            var user = this.Tag as NhanVien;
            if (user != null)
            {
                // Lưu chức vụ vào biến tạm
                string chucVuDangNhap = user.ChucVu?.Trim();

                // Nếu KHÔNG phải Quản lý thì ẩn hoặc khóa nút Xóa
                if (!string.Equals(chucVuDangNhap, "Quan ly", StringComparison.OrdinalIgnoreCase))
                {
                    btnXoaNV.Enabled = false; // Ẩn nút xóa
                    btnThemNV.Enabled = false; // Ẩn nút Thêm
                }
            }
            // Populate combobox - LƯU VÀ HIỂN THỊ KHÔNG DẤU
            cbLoaiPhong.Items.Clear();
            cbLoaiPhong.Items.AddRange(new string[] { "Don", "Doi", "Bon" });
            cbLoaiPhong.SelectedIndex = 0;

            cbTinhTrang.Items.Clear();
            cbTinhTrang.Items.AddRange(new string[] { "Chua thue", "Da thue" });
            cbLoaiPhong.SelectedIndex = -1;

            cbTgThue.Items.Clear();
            cbTgThue.Items.AddRange(new string[]
            {
                    "0",   // 0: không chọn giờ (sử dụng khi chọn ngày)
                    "1",   // 0–1h = 60k
                    "3",   // 1–3h = 150k
                    "12"   // 3–12h = 400k
            });
            // Không chọn mặc định
            cbTgThue.SelectedIndex = -1;


            // Đảm bảo textbox là single-line (Enter sẽ gửi KeyDown)
            if (txtSoPhong != null)
            {
                txtSoPhong.Multiline = false;
                // Nếu form có AcceptButton, tắt nó để Enter không bị bắt bởi AcceptButton
                this.AcceptButton = null;

                // Gắn event KeyDown (nếu chưa gắn)
                txtSoPhong.KeyDown -= txtSoPhong_KeyDown;
                txtSoPhong.KeyDown += txtSoPhong_KeyDown;

                // Focus để tiện nhập số phòng + Enter
                txtSoPhong.Focus();
            }
            if (cbGioiTinh != null)
            {
                cbGioiTinh.Items.Clear();
                cbGioiTinh.Items.AddRange(new string[] { "Nam", "Nữ", "Khác" });
                cbGioiTinh.SelectedIndex = -1; // mặc định Nam
            }
            dtpNgayThue.Format = DateTimePickerFormat.Custom;
            dtpNgayThue.CustomFormat = "dd/MM/yyyy HH:mm";
            dtpNgayThue.ShowUpDown = true;
            dtpNgayThue.Value = DateTime.Now;


            dtpNgaySinh.Format = DateTimePickerFormat.Custom;
            dtpNgaySinh.CustomFormat = "dd/MM/yyyy";
            dtpNgaySinh.ShowUpDown = true;

            // load data
            LoadPhong();
            LoadDanhSachKhachHang();
            LoadNhanVien();

            dtgTraPhong.DefaultCellStyle.Font = new Font("Segoe UI", 8);
            dtgTraPhong.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8, FontStyle.Bold);

            dtgQLP.DefaultCellStyle.Font = new Font("Segoe UI", 8);
            dtgQLP.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8, FontStyle.Bold);

            dtgCTKH.DefaultCellStyle.Font = new Font("Segoe UI", 8);
            dtgCTKH.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8, FontStyle.Bold);

            dtgTTNV.DefaultCellStyle.Font = new Font("Segoe UI", 8);
            dtgTTNV.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8, FontStyle.Bold);

            cbTgThue.DropDownStyle = ComboBoxStyle.DropDownList;
            cbGioiTinh.DropDownStyle = ComboBoxStyle.DropDownList;

            var doanhThuRecord = db.TongDoanhThus.FirstOrDefault();

            if (doanhThuRecord != null)
            {
                decimal tongTien = doanhThuRecord.TongTien; // vì không nullable
                txtDoanhThu.Text = tongTien.ToString("N0") + " VNĐ"; // 2 chữ số thập phân
            }
            else
            {
                txtDoanhThu.Text = "0 VNĐ";
            }
            if (dtpTraP != null) // Thay dtpTraP bằng tên thật của control
            {
                dtpTraP.Value = DateTime.Now;
                dtpTraP.Checked = true; // Nếu bạn dùng ShowCheckbox
            }

            // Đặt cỡ chữ mặc định cho tất cả DataGridViews
            float desiredFontSize = 12f; // Cỡ chữ bạn muốn (16)
            Font dgvFont = new Font(this.Font.FontFamily, desiredFontSize, FontStyle.Regular); // Tạo Font mới

            // Liệt kê tất cả DataGridViews của bạn
            var allDgv = new[] { dtgQLP, dtgCTKH, dtgTraPhong, dtgTTNV, dtgDichVu };

            foreach (var dgv in allDgv)
            {
                if (dgv != null)
                {
                    // Đặt Font cho toàn bộ DataGridView
                    dgv.DefaultCellStyle.Font = dgvFont;
                    // Đặt Font cho tiêu đề cột (nếu muốn nó cùng cỡ)
                    dgv.ColumnHeadersDefaultCellStyle.Font = new Font(dgvFont, FontStyle.Bold);
                    // Tự động điều chỉnh chiều cao hàng cho vừa với font mới
                    dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
                }
            }
        }

        // 1 biến dùng tạm để lưu đường dẫn file đang chọn (mỗi tab có tag riêng)
        private Dictionary<string, string> attachmentPaths = new Dictionary<string, string>();

        private void btnDinhKem_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            string suffix = btn.Name.Last().ToString(); // Lấy số tab: "1" hoặc "2"
            Label lbl = this.Controls.Find($"lblTenFile{suffix}", true).FirstOrDefault() as Label;

            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Word/Excel/All files|*.doc;*.docx;*.xls;*.xlsx;*.*";
                ofd.Multiselect = false;

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string filePath = ofd.FileName;
                    string fileName = Path.GetFileName(filePath);

                    // Lưu file theo tab
                    attachmentPaths[$"tab{suffix}"] = filePath;

                    // Cập nhật label
                    if (lbl != null)
                        lbl.Text = fileName;
                }
            }
        }

        private void btnXoaFile_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            string suffix = btn.Name.Last().ToString(); // Lấy "1" hoặc "2"
            Label lbl = this.Controls.Find($"lblTenFile{suffix}", true).FirstOrDefault() as Label;

            // Xóa file khỏi dictionary
            attachmentPaths.Remove($"tab{suffix}");

            // Reset label
            if (lbl != null)
                lbl.Text = "(Chưa chọn tệp)";
        }

        private void ResetFileAttachments()
        {
            // Reset tab 1
            if (attachmentPaths.ContainsKey("tab1"))
                attachmentPaths["tab1"] = null;
            if (lblTenFile1 != null)
                lblTenFile1.Text = "(Chưa chọn tệp)";

            // Reset tab 2
            if (attachmentPaths.ContainsKey("tab2"))
                attachmentPaths["tab2"] = null;
            if (lblTenFile2 != null)
                lblTenFile2.Text = "(Chưa chọn tệp)";
        }

        // TRẢ MỘT PHÒNG
        private void TraMotPhong()
        {
            // Nếu có nhiều hàng được chọn -> gọi phương thức xử lý nhiều
            if (dtgTraPhong != null && dtgTraPhong.SelectedRows.Count > 1)
            {
                TraNhieuPhong();
                return;
            }

            string tenKH = txtTenKHTraP.Text.Trim();
            string soPhong = txtSoPTra.Text.Trim();
            DateTime actualReturn = dtpTraP != null ? dtpTraP.Value : DateTime.Now;

            try
            {
                // GỌI HÀM CỐT LÕI MỚI
                decimal tienPhong = TraPhongKhongThongBao(tenKH, soPhong, actualReturn);

                if (tienPhong < 0)
                {
                    MessageBox.Show("Không tìm thấy khách hàng hoặc phòng không khớp.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                db.SaveChanges(); // LƯU DB 1 LẦN

                // === Cập nhật UI ===
                var doanhThuRecord = db.TongDoanhThus.FirstOrDefault();
                if (doanhThuRecord != null)
                {
                    txtDoanhThu.Text = doanhThuRecord.TongTien.ToString("N0") + " VNĐ";
                }

                LoadDanhSachKhachHang();
                LoadTraPhongGrid("", "");
                LoadPhong();

                if (dtgCTKH != null) { dtgCTKH.Refresh(); dtgCTKH.ClearSelection(); }
                if (dtgTraPhong != null) { dtgTraPhong.Refresh(); dtgTraPhong.ClearSelection(); }
                if (dtgQLP != null) { dtgQLP.Refresh(); dtgQLP.ClearSelection(); }

                txtTenKHTraP.Clear();
                txtSoPTra.Clear();

                MessageBox.Show($"Phòng {soPhong} đã được trả. Tiền phòng {tienPhong:N0} VNĐ đã cộng vào doanh thu.\nThời gian: {actualReturn:dd/MM/yyyy HH:mm}",
                    "Trả phòng", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi xử lý trả phòng: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        private void TraNhieuPhong()
        {
            if (dtgTraPhong.SelectedRows.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một phòng để trả (giữ CTRL hoặc SHIFT).");
                return;
            }

            int count = 0;
            decimal tongTienDaThu = 0m;
            List<string> danhSachPhongDaTra = new List<string>();

            // SỬA LỖI: Lấy Ngày trả phòng (sử dụng DateTime.Now nếu dtpTraP không tồn tại)
            DateTime actualReturn = dtpTraP != null ? dtpTraP.Value : DateTime.Now;

            try
            {

                // Bạn có thể convert sang List để đảm bảo không bị lỗi trong quá trình lặp
                var selectedRowsList = dtgTraPhong.SelectedRows.Cast<DataGridViewRow>().ToList();

                foreach (DataGridViewRow row in selectedRowsList)
                {
                    // BỎ QUA hàng trống/mới nếu có
                    if (row.IsNewRow) continue;

                    // SỬA LỖI: Sử dụng .Value?.ToString() ?? "" để tránh lỗi null
                    string tenKH = row.Cells["TenKhachHang"].Value?.ToString() ?? "";
                    string soPhong = row.Cells["SoPhong"].Value?.ToString() ?? "";

                    // GỌI HÀM CỐT LÕI MỚI (có 3 tham số và trả về decimal)
                    decimal tienPhong = TraPhongKhongThongBao(tenKH, soPhong, actualReturn);

                    if (tienPhong >= 0)
                    {
                        count++;
                        tongTienDaThu += tienPhong;
                        danhSachPhongDaTra.Add(soPhong);
                    }
                }

                db.SaveChanges(); // LƯU TẤT CẢ 1 LẦN

                // === Cập nhật UI & Thông báo ===
                LoadDanhSachKhachHang();
                LoadPhong();
                LoadTraPhongGrid("", "");

                var doanhThuRecord = db.TongDoanhThus.FirstOrDefault();
                if (doanhThuRecord != null)
                {
                    txtDoanhThu.Text = doanhThuRecord.TongTien.ToString("N0") + " VNĐ";
                }

                // HIỂN THỊ THÔNG BÁO TỔNG HỢP
                if (count > 0)
                {
                    string danhSachPhong = string.Join(", ", danhSachPhongDaTra);
                    string message = $"✅ Đã trả **{count}** phòng thành công!\n" +
                                     $"Danh sách phòng: {danhSachPhong}\n" +
                                     $"💰 Tổng tiền thu được: **{tongTienDaThu:N0} VNĐ**";

                    MessageBox.Show(message, "Thông báo Trả Phòng Thành Công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Không có phòng nào được trả thành công trong các hàng đã chọn.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lưu dữ liệu trả nhiều phòng: " + ex.Message, "Lỗi Database", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // TRẢ PHÒNG KHÔNG THÔNG BÁO - Trả về tiền phòng nếu thành công, hoặc -1 nếu thất bại.
        // TRẢ PHÒNG KHÔNG THÔNG BÁO - Trả về tiền phòng nếu thành công, hoặc -1 nếu thất bại.
        private decimal TraPhongKhongThongBao(string tenKH, string soPhong, DateTime actualReturn)
        {
            if (string.IsNullOrEmpty(tenKH) || string.IsNullOrEmpty(soPhong))
                return -1;

            try
            {
                // 1. Tìm khách hàng gần nhất liên quan đến phòng này (KHÔNG check NgayTra == null)
                var khToCheckOut = db.KhachHangs
                                        .Where(k => k.SoPhong == soPhong && k.TenKhachHang == tenKH)
                                        .OrderByDescending(k => k.NgayThue) // Lấy người mới nhất khớp tên và phòng
                                        .FirstOrDefault();

                if (khToCheckOut == null)
                {
                    Console.WriteLine($"DEBUG: Khong tim thay KH '{tenKH}' o phong '{soPhong}' de tra."); // Thêm log
                    return -1; // Không tìm thấy khách phù hợp
                }

                // CHỈ CẬP NHẬT NẾU HỌ CHƯA TRẢ PHÒNG
                // (Tránh trường hợp click trả phòng 2 lần cho cùng 1 người)
                if (khToCheckOut.NgayTra != null)
                {
                    Console.WriteLine($"DEBUG: KH '{tenKH}' o phong '{soPhong}' da tra roi.");
                    // Có thể trả về 0 hoặc -1 tùy bạn muốn
                    return 0; // Đã trả rồi, không cộng thêm doanh thu
                }

                var phong = db.Phongs.FirstOrDefault(p => p.SoPhong == soPhong);
                if (phong == null)
                {
                    Console.WriteLine($"DEBUG: Khong tim thay Phong '{soPhong}' de tra."); // Thêm log
                    return -1;
                }

                // 2. Lấy tiền phải trả TỪ PHÒNG (vì nó đã tổng hợp cả dịch vụ)
                decimal tienPhaiTra = phong.Gia ?? 0;

                // 3. Cộng dồn doanh thu (Logic này giữ nguyên)
                var doanhThuRecord = db.TongDoanhThus.FirstOrDefault();
                if (doanhThuRecord == null)
                {
                    doanhThuRecord = new TongDoanhThu { NgayCapNhat = DateTime.Now, TongTien = tienPhaiTra };
                    db.TongDoanhThus.Add(doanhThuRecord);
                }
                else
                {
                    doanhThuRecord.TongTien += tienPhaiTra;
                    doanhThuRecord.NgayCapNhat = DateTime.Now;
                }

                // 4. Cập nhật phòng về trạng thái trống (Logic này giữ nguyên)
                phong.TinhTrang = "Chua thue";
                phong.NgayTraPhong = actualReturn;
                phong.SoGioThue = 0;
                phong.Gia = 0;

                // --- BƯỚC 5: (THAY ĐỔI) CẬP NHẬT NGÀY TRẢ CHO KHÁCH HÀNG ---
                // THAY VÌ XÓA, CHÚNG TA CẬP NHẬT NgayTra
                khToCheckOut.NgayTra = actualReturn;

                return tienPhaiTra;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong TraPhongKhongThongBao: {ex.ToString()}"); // Log lỗi chi tiết hơn
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.ToString()}");
                }
                return -1; // Trả về -1 nếu có lỗi
            }
        }

        private void btnTraPhong_Click(object sender, EventArgs e)
        {
            // --- XỬ LÝ TRẢ NHIỀU PHÒNG ---
            if (dtgTraPhong != null && dtgTraPhong.SelectedRows.Count > 1)
            {
                // Tạo danh sách model từ các hàng được chọn
                var selectedRows = dtgTraPhong.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index).ToList();
                var models = new List<InvoicePrinter.InvoicePrintModel>();

                // *** XÁC NHẬN CHO NHIỀU PHÒNG (ĐÃ CÓ SẴN) ***
                var confirmMulti = MessageBox.Show(
                     $"Bạn có chắc chắn muốn trả {selectedRows.Count} phòng đã chọn không?",
                     "Xác nhận trả nhiều phòng",
                     MessageBoxButtons.YesNo,
                     MessageBoxIcon.Question);
                if (confirmMulti != DialogResult.Yes)
                    return; // Dừng nếu không đồng ý

                // (Code chuẩn bị model, gộp hóa đơn, hiển thị preview cho nhiều phòng giữ nguyên...)
                foreach (var row in selectedRows)
                {
                    if (row.IsNewRow) continue;
                    string ten = row.Cells["TenKhachHang"].Value?.ToString() ?? "";
                    string so = row.Cells["SoPhong"].Value?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(so)) continue;
                    var m = PrepareInvoiceModel(ten, so);
                    models.Add(m);
                }

                if (!models.Any())
                {
                    MessageBox.Show("Không tìm thấy dữ liệu hóa đơn để in.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var aggregate = new InvoicePrinter.InvoicePrintModel
                {
                    TenKhach = "Tổng nhiều phòng",
                    SoPhong = string.Join(", ", models.Select(m => m.SoPhong).Where(s => !string.IsNullOrWhiteSpace(s))),
                    NgayThue = models.Min(m => m.NgayThue),
                    NgayTra = models.Max(m => m.NgayTra),
                    RoomOnly = models.Sum(m => m.RoomOnly),
                    RoomTotal = models.Sum(m => m.RoomTotal),
                    Services = new List<InvoicePrinter.ServiceLine>()
                };

                var allServices = models.SelectMany(m => m.Services ?? Enumerable.Empty<InvoicePrinter.ServiceLine>());
                var grouped = allServices
                    .GroupBy(s => new { Name = s.TenDV ?? "", Price = s.DonGia })
                    .Select(g => new InvoicePrinter.ServiceLine
                    {
                        TenDV = g.Key.Name,
                        DonGia = g.Key.Price,
                        SoLuong = g.Sum(x => x.SoLuong),
                        ThanhTien = g.Sum(x => x.ThanhTien)
                    })
                    .ToList();
                aggregate.Services.AddRange(grouped);

                invoicesToPrint = new List<InvoicePrinter.InvoicePrintModel> { aggregate };
                invoicePrintIndex = 0;

                using (var preview = new PrintPreviewDialog())
                {
                    preview.Document = printDocument1;
                    preview.WindowState = FormWindowState.Maximized;
                    preview.ShowDialog();
                }

                // Sau khi in, thực hiện trả nhiều phòng như trước
                TraNhieuPhong(); // Gọi sau khi preview
                return; // Kết thúc xử lý nhiều phòng
            }

            // --- XỬ LÝ TRẢ MỘT PHÒNG ---
            string tenKH_single = (txtTenKHTraP != null && !string.IsNullOrWhiteSpace(txtTenKHTraP.Text)) ? txtTenKHTraP.Text.Trim()
                                   : (txtTenKH != null ? txtTenKH.Text.Trim() : ""); // Lấy từ textbox tab đăng ký nếu textbox trả phòng trống
            string soPhong_single = (txtSoPTra != null && !string.IsNullOrWhiteSpace(txtSoPTra.Text)) ? txtSoPTra.Text.Trim()
                                    : (txtSoPhong != null ? txtSoPhong.Text.Trim() : ""); // Lấy từ textbox tab QLP nếu textbox trả phòng trống


            // Lấy từ dòng chọn duy nhất nếu textbox vẫn trống
            if (string.IsNullOrWhiteSpace(tenKH_single) && string.IsNullOrWhiteSpace(soPhong_single) && dtgTraPhong != null && dtgTraPhong.SelectedRows.Count == 1)
            {
                var row = dtgTraPhong.SelectedRows[0];
                if (!row.IsNewRow) // Đảm bảo không phải dòng mới
                {
                    tenKH_single = row.Cells["TenKhachHang"].Value?.ToString() ?? "";
                    soPhong_single = row.Cells["SoPhong"].Value?.ToString() ?? "";
                }
            }

            // Kiểm tra lại thông tin sau khi đã cố gắng lấy từ nhiều nguồn
            if (string.IsNullOrEmpty(tenKH_single) || string.IsNullOrEmpty(soPhong_single))
            {
                MessageBox.Show("Vui lòng nhập/chọn tên khách hàng và số phòng hợp lệ.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // *** THÊM XÁC NHẬN CHO MỘT PHÒNG ***
            var confirmSingle = MessageBox.Show(
                 $"Bạn có chắc chắn muốn trả phòng {soPhong_single} cho khách '{tenKH_single}' không?",
                 "Xác nhận trả phòng",
                 MessageBoxButtons.YesNo,
                 MessageBoxIcon.Question);

            if (confirmSingle != DialogResult.Yes)
                return; // Dừng nếu không đồng ý

            // Tiếp tục chuẩn bị model và in/preview (code giữ nguyên như bạn gửi)
            var single = PrepareInvoiceModel(tenKH_single, soPhong_single);
            invoicesToPrint = new List<InvoicePrinter.InvoicePrintModel> { single };
            invoicePrintIndex = 0;

            using (var preview = new PrintPreviewDialog())
            {
                preview.Document = printDocument1;
                preview.WindowState = FormWindowState.Maximized;
                preview.ShowDialog();
            }

            // Sau khi preview, thực hiện trả phòng (một)
            TraMotPhong(); // Gọi sau khi preview
        }

        private (decimal gia, int soGio) CalculatePriceFromSelection(string loaiPhong, string cbTgThueText, int soNgay)
        {
            decimal gia = 0m;
            int soGio = 0;

            loaiPhong = NormalizeLoaiPhong(loaiPhong ?? "");

            if (soNgay > 0)
            {
                decimal gia1Ngay = TinhGia(loaiPhong, "24");
                gia = gia1Ngay * soNgay;
                soGio = 24 * soNgay;
            }
            else
            {
                if (int.TryParse(cbTgThueText, out int tg) && tg > 0)
                {
                    gia = TinhGia(loaiPhong, cbTgThueText);
                    soGio = tg;
                }
            }

            return (gia, soGio);
        }


        private void txtNgay_TextChanged(object sender, EventArgs e)
        {
            // chỉ cho phép số nguyên >=0 (loại bỏ ký tự khác)
            var tb = sender as TextBox;
            if (tb == null) return;

            string cleaned = System.Text.RegularExpressions.Regex.Replace(tb.Text ?? "", @"[^\d]", "");
            if (tb.Text != cleaned) { tb.Text = cleaned; tb.SelectionStart = cleaned.Length; }

            int soNgay = 0;
            if (!int.TryParse(tb.Text, out soNgay)) soNgay = 0;

            // Nếu đã chọn ngày >0 -> set cbTgThue về "0" và disable cbTgThue
            if (soNgay > 0)
            {
                if (cbTgThue != null)
                {
                    // tìm item "0" và chọn nó (nếu tồn tại)
                    for (int i = 0; i < cbTgThue.Items.Count; i++)
                    {
                        if (cbTgThue.Items[i]?.ToString() == "0")
                        {
                            cbTgThue.SelectedIndex = i;
                            break;
                        }
                    }
                    cbTgThue.Enabled = false;
                }
            }
            else
            {
                if (cbTgThue != null) cbTgThue.Enabled = true;
            }

            // Tính giá và hiển thị
            string loai = NormalizeLoaiPhong(cbLoaiPhong?.Text?.Trim() ?? "");
            var (gia, soGio) = CalculatePriceFromSelection(loai, cbTgThue?.Text ?? "0", soNgay);
            txtGia.Text = gia > 0 ? gia.ToString("N0") : "";
        }

        private void InitializeServiceTab()
        {
            // Cache danh sách dịch vụ
            LoadAllDichVuCache();

            // Cấu hình DataGridView
            ConfigDtgDichVu();

            // Tải danh sách các loại dịch vụ chính (Ăn Sáng, Đồ Uống...)
            LoadLoaiDichVu();

            // Gán sự kiện cho các control
            WireUpServiceEvents();

            // Vô hiệu hóa control cho đến khi chọn phòng
            SetDichVuControlsEnabled(false);
            dtgDichVu.MultiSelect = true;
            dtgDichVu.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            if (dtgDichVu != null)
            {
                dtgDichVu.CellClick -= dtgDichVu_CellClick; // Tránh gán nhiều lần
                dtgDichVu.CellClick += dtgDichVu_CellClick;
            }
            if (cbLoaiDV != null)
            {
                cbLoaiDV.DropDownStyle = ComboBoxStyle.DropDownList; // Chỉ cho chọn, không cho nhập
            }
            if (cbDV != null)
            {
                cbDV.DropDownStyle = ComboBoxStyle.DropDownList; // Chỉ cho chọn, không cho nhập
            }
            SetDichVuControlsEnabled(false);
        }
        private void WireUpServiceEvents()
        {
            // Sự kiện khi thay đổi text trong ô Số Phòng
            this.txtSoPDV.TextChanged += new System.EventHandler(this.TxtSoPDV_TextChanged);

            // Sự kiện khi chọn Loại Dịch Vụ
            this.cbLoaiDV.SelectedIndexChanged += new System.EventHandler(this.CbLoaiDV_SelectedIndexChanged);

            // Sự kiện khi chọn Dịch Vụ Cụ Thể
            this.cbDV.SelectedIndexChanged += new System.EventHandler(this.CbDV_SelectedIndexChanged);

            // Sự kiện khi nhấn nút "Thêm"
            this.btnThemDV.Click += new System.EventHandler(this.BtnThemDV_Click);
        }

        // 2. Cấu hình cột cho DataGridView
        private void ConfigDtgDichVu()
        {
            dtgDichVu.AutoGenerateColumns = false;
            dtgDichVu.Columns.Clear();

            // Cột STT (Lưu STT của bảng ChiTietSuDungDV để dùng cho việc Xóa)
            if (!dtgDichVu.Columns.Contains("ColSTT"))
            {
                var colSTT_DB = new DataGridViewTextBoxColumn
                {
                    Name = "ColSTT",
                    HeaderText = "STT_DB",
                    DataPropertyName = "STT",
                    Visible = false
                };
                dtgDichVu.Columns.Add(colSTT_DB);
            }
            if (!dtgDichVu.Columns.Contains("ColSTTDisplay"))
            {
                var colSTTDisplay = new DataGridViewTextBoxColumn
                {
                    Name = "ColSTTDisplay",
                    HeaderText = "STT",
                    Width = 40,
                    ReadOnly = true
                };
                // Chèn vào vị trí đầu tiên nếu muốn
                dtgDichVu.Columns.Insert(0, colSTTDisplay);
            }

            // Cột Tên Dịch Vụ
            var colTenDV = new DataGridViewTextBoxColumn
            {
                Name = "ColTenDV",
                HeaderText = "Tên dịch vụ",
                Width = 200
            };
            dtgDichVu.Columns.Add(colTenDV);

            // Cột Số Lượng
            var colSoLuong = new DataGridViewTextBoxColumn
            {
                Name = "ColSoLuong",
                HeaderText = "Số lượng",
                Width = 80
            };
            dtgDichVu.Columns.Add(colSoLuong);

            // Cột Đơn Giá
            var colDonGia = new DataGridViewTextBoxColumn
            {
                Name = "ColDonGia",
                HeaderText = "Đơn giá",
                Width = 100,
                DefaultCellStyle = { Format = "N0" } // Định dạng số
            };
            dtgDichVu.Columns.Add(colDonGia);

            // Cột Thành Tiền
            var colThanhTien = new DataGridViewTextBoxColumn
            {
                Name = "ColThanhTien",
                HeaderText = "Thành tiền",
                Width = 120,
                DefaultCellStyle = { Format = "N0" }
            };
            dtgDichVu.Columns.Add(colThanhTien);

            // Cài đặt cho dtgDichVu
            dtgDichVu.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dtgDichVu.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dtgDichVu.MultiSelect = false;
            dtgDichVu.ReadOnly = true;
        }

        // 3. Tải cache và ComboBox
        private void LoadAllDichVuCache()
        {
            // Tải tất cả dịch vụ vào một biến list để dùng lại
            allDichVuList = db.DichVus.Where(dv => dv.Active == true).ToList();
        }

        private void LoadLoaiDichVu()
        {
            // Lấy các loại dịch vụ duy nhất từ cache
            var loaiDichVuList = allDichVuList
                                   .Select(dv => dv.LoaiDichVu)
                                   .Distinct()
                                   .OrderBy(loai => loai)
                                   .ToList();
            cbLoaiDV.DataSource = loaiDichVuList;
            cbLoaiDV.SelectedIndex = -1;
            cbLoaiDV.DisplayMember = "LoaiDichVu";
        }

        // 4. Các hàm bật/tắt và xóa control
        private void SetDichVuControlsEnabled(bool isEnabled)
        {
            cbLoaiDV.Enabled = isEnabled;
            cbDV.Enabled = isEnabled;
            txtSoLuongDV.Enabled = isEnabled;
            btnThemDV.Enabled = isEnabled;
            // btnXoaDV.Enabled = isEnabled; // Bật/tắt nút xóa (nếu có)
        }

        // 5. Tải dữ liệu vào DataGridView
        private void LoadDichVuDaSuDung(int maKhachHang)
        {
            // --- BƯỚC 1: Reset trạng thái sửa và danh sách tạm ---
            rowBeingEdited = null; // Không còn dòng nào đang được sửa
            if (btnThemDV != null) btnThemDV.Text = "Thêm"; // Đặt lại nút về "Thêm"
            danhSachDvMoiThem.Clear(); // Xóa các dịch vụ mới chưa lưu khỏi danh sách tạm

            try
            {
                // --- BƯỚC 2: Truy vấn CSDL để lấy dịch vụ đã lưu ---
                var dvDaDung = db.ChiTietSuDungDVs
                                 .Where(ct => ct.MaKhachHang == maKhachHang)
                                 .Include(ct => ct.DichVu) // Rất quan trọng để lấy TenDV, DonGia
                                 .OrderBy(ct => ct.NgaySuDung) // Sắp xếp theo thời gian sử dụng (tùy chọn)
                                 .ToList();

                // --- BƯỚC 3: Đổ dữ liệu vào DataGridView ---
                LoadDichVuVaoDtg(dvDaDung); // Gọi hàm để hiển thị lên grid

                // --- BƯỚC 4: Đánh lại số thứ tự hiển thị ---
                RenumberDtgDichVuSTT();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh sách dịch vụ đã sử dụng: {ex.Message}", "Lỗi Database", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Console.WriteLine($"Lỗi LoadDichVuDaSuDung: {ex.ToString()}");
                // Nếu lỗi, xóa sạch grid và tổng tiền
                if (dtgDichVu != null) dtgDichVu.Rows.Clear();
                if (txtTongDV != null) txtTongDV.Text = "Lỗi tải DV";
            }
        }

        // Hàm này sẽ đổ dữ liệu vào dtgDichVu và cập nhật txtTongDV
        private void LoadDichVuVaoDtg(List<ChiTietSuDungDV> listFromDb)
        {
            // Kiểm tra control trước khi thao tác
            if (dtgDichVu == null || dtgDichVu.Columns.Contains("ColSTTDisplay") == false || dtgDichVu.Columns.Contains("ColSTT") == false)
            {
                Console.WriteLine("Lỗi LoadDichVuVaoDtg: dtgDichVu hoặc các cột STT chưa được cấu hình.");
                return;
            }

            dtgDichVu.Rows.Clear(); // Xóa sạch các dòng cũ trên grid

            // Kiểm tra danh sách đầu vào
            if (listFromDb == null || !listFromDb.Any())
            {
                // Nếu không có dữ liệu, chỉ cần tính lại tổng (sẽ ra 0)
                RecalculateTotalServiceCost();
                return;
            }

            // Duyệt qua danh sách dịch vụ lấy từ DB
            foreach (var item in listFromDb)
            {
                // Lấy thông tin Tên và Đơn giá từ Navigation Property (item.DichVu) đã được Include
                string tenDV = item.DichVu?.TenDV;
                decimal donGia = item.DichVu?.DonGia ?? 0;

                // Fallback: Nếu vì lý do nào đó item.DichVu là null, thử tìm trong cache
                if (string.IsNullOrEmpty(tenDV) && allDichVuList != null)
                {
                    var dvInfo = allDichVuList.FirstOrDefault(dv => dv.MaDV == item.MaDV);
                    if (dvInfo != null)
                    {
                        tenDV = dvInfo.TenDV;
                        donGia = dvInfo.DonGia;
                    }
                }

                // Chỉ thêm vào grid nếu có Tên Dịch Vụ hợp lệ
                if (!string.IsNullOrEmpty(tenDV))
                {
                    // Thêm dòng mới vào grid
                    int rowIndex = dtgDichVu.Rows.Add(
                        0, // STT hiển thị (sẽ được đánh lại bởi RenumberDtgDichVuSTT)
                        item.STT, // STT thực tế từ DB (cột ẩn ColSTT)
                        tenDV,
                        item.SoLuong,
                        donGia,
                        item.ThanhTien
                    );

                    // Gán đối tượng ChiTietSuDungDV vào Tag của dòng vừa thêm
                    // Tag này có thể hữu ích cho việc lấy lại đối tượng đầy đủ sau này (ví dụ khi xóa hoặc sửa)
                    if (rowIndex >= 0)
                    {
                        dtgDichVu.Rows[rowIndex].Tag = item;
                    }
                }
                else
                {
                    Console.WriteLine($"Cảnh báo LoadDichVuVaoDtg: Không tìm thấy tên cho MaDV={item.MaDV}, STT={item.STT}");
                }
            }

            // Sau khi thêm tất cả các dòng, tính lại tổng tiền từ grid
            RecalculateTotalServiceCost();
        }

        // --- CÁC HÀM XỬ LÝ SỰ KIỆN ---
        // 1. Sửa TxtSoPDV_TextChanged:
        private void TxtSoPDV_TextChanged(object sender, EventArgs e)
        {
            // --- Reset trạng thái ban đầu ---
            currentMaKhachHang = null;
            if (txtTenKH != null) txtTenKH.Text = "";
            SetDichVuControlsEnabled(false);
            ClearDichVuControlsForNewEntry(); // Chỉ reset phần nhập mới
            if (txtTongDV != null) txtTongDV.Text = "0 VNĐ"; // Reset tổng DV về 0
            if (dtgDichVu != null) dtgDichVu.Rows.Clear(); // Xóa grid DV

            string soPhong = txtSoPDV.Text.Trim();
            if (string.IsNullOrEmpty(soPhong)) return;

            // --- BƯỚC 1: Tìm thông tin phòng ---
            var phong = db.Phongs.FirstOrDefault(p => p.SoPhong == soPhong);
            if (phong == null)
            {
                if (txtTongDV != null) txtTongDV.Text = "Phòng không tồn tại";
                return;
            }

            // --- BƯỚC 2: Kiểm tra Tình trạng phòng ---
            // Chỉ xử lý nếu phòng đang "Da thue"
            if (phong.TinhTrang != null && phong.TinhTrang.Equals("Da thue", StringComparison.OrdinalIgnoreCase))
            {
                // --- BƯỚC 3: Tìm khách hàng GẦN NHẤT liên quan (BỎ check NgayTra == null) ---
                var khachHangLienQuan = db.KhachHangs
                                         .Where(kh => kh.SoPhong == soPhong)
                                         .OrderByDescending(kh => kh.NgayThue) // Lấy người mới nhất
                                         .FirstOrDefault();

                // --- BƯỚC 4: Xử lý dựa trên khách hàng tìm được ---
                if (khachHangLienQuan != null)
                {
                    currentMaKhachHang = khachHangLienQuan.STT;
                    if (txtTenKH != null) txtTenKH.Text = khachHangLienQuan.TenKhachHang;
                    SetDichVuControlsEnabled(true);
                    LoadDichVuDaSuDung(khachHangLienQuan.STT); // Tải dịch vụ đã dùng (nếu có)
                }
                else
                {
                    // Lỗi: Phòng "Da thue" nhưng không có bản ghi KhachHang nào liên quan
                    currentMaKhachHang = null;
                    if (txtTenKH != null) txtTenKH.Text = "";
                    SetDichVuControlsEnabled(false);
                    ClearDichVuControlsForNewEntry();
                    if (txtTongDV != null) txtTongDV.Text = "Lỗi: Không tìm thấy KH";
                    MessageBox.Show($"Phòng {soPhong} 'Đã thuê' nhưng không có KH liên quan.", "Dữ liệu lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else // Phòng không "Da thue"
            {
                currentMaKhachHang = null;
                if (txtTenKH != null) txtTenKH.Text = "";
                SetDichVuControlsEnabled(false);
                ClearDichVuControlsForNewEntry();
                if (txtTongDV != null) txtTongDV.Text = "Phòng trống";
                if (dtgDichVu != null) dtgDichVu.Rows.Clear(); // Xóa grid DV nếu phòng trống
            }
        }

        // Sự kiện khi chọn Loại Dịch Vụ (Ăn Sáng, Giặt Sấy...)
        private void CbLoaiDV_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbLoaiDV.SelectedItem == null)
            {
                cbDV.DataSource = null;
                return;
            }

            string selectedLoaiDV = cbLoaiDV.SelectedItem.ToString();

            // Lọc danh sách dịch vụ cụ thể từ cache
            var dichVuList = allDichVuList
                               .Where(dv => dv.LoaiDichVu == selectedLoaiDV)
                               .OrderBy(dv => dv.TenDV)
                               .ToList();

            cbDV.DataSource = dichVuList;
            cbDV.DisplayMember = "TenDV";
            cbDV.ValueMember = "MaDV"; // Lưu MaDV làm giá trị
            cbDV.SelectedIndex = -1;

            // Reset các ô thông tin
            txtDonGiaDV.Text = "";
            txtDonViTinh.Text = "";
            txtSoLuongDV.Text = "1";
        }

        // Sự kiện khi chọn Dịch Vụ Cụ Thể (Phở, Giặt Quần Áo...)
        private void CbDV_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbDV.SelectedItem == null)
            {
                txtDonGiaDV.Text = "";
                txtDonViTinh.Text = "";
                return;
            }

            // Lấy đối tượng DichVu đã chọn
            DichVu selectedDV = cbDV.SelectedItem as DichVu;
            if (selectedDV != null)
            {
                txtDonGiaDV.Text = selectedDV.DonGia.ToString("N0");
                txtDonViTinh.Text = selectedDV.DonViTinh;
            }
        }

        // Sự kiện khi nhấn nút THÊM DỊCH VỤ
        private void BtnThemDV_Click(object sender, EventArgs e)
        {
            // 1. Kiểm tra điều kiện cơ bản (khách hàng, dịch vụ, số lượng)
            if (currentMaKhachHang == null) { /* ... lỗi ... */ return; }
            if (cbDV.SelectedItem == null) { /* ... lỗi ... */ return; }
            if (!decimal.TryParse(txtSoLuongDV.Text, out decimal soLuong) || soLuong <= 0) { /* ... lỗi ... */ return; }

            DichVu selectedDV = cbDV.SelectedItem as DichVu;
            if (selectedDV == null) return;

            // 2. KIỂM TRA XEM ĐANG SỬA HAY THÊM MỚI
            if (rowBeingEdited != null)
            {
                // --- CHẾ ĐỘ CẬP NHẬT ---
                try
                {
                    ChiTietSuDungDV itemToUpdate = rowBeingEdited.Tag as ChiTietSuDungDV;

                    if (itemToUpdate != null)
                    {
                        // Cập nhật thông tin cho đối tượng trong danh sách tạm
                        itemToUpdate.MaDV = selectedDV.MaDV;
                        itemToUpdate.SoLuong = soLuong;
                        itemToUpdate.ThanhTien = CalculateThanhTien(selectedDV.DonGia, soLuong);
                        itemToUpdate.DichVu = selectedDV; // Cập nhật cả navigation property

                        // Cập nhật trực tiếp các ô trên dòng DataGridView đang sửa
                        rowBeingEdited.Cells["ColTenDV"].Value = selectedDV.TenDV;
                        rowBeingEdited.Cells["ColSoLuong"].Value = soLuong;
                        rowBeingEdited.Cells["ColDonGia"].Value = selectedDV.DonGia; // Cập nhật đơn giá nếu DV thay đổi
                        rowBeingEdited.Cells["ColThanhTien"].Value = itemToUpdate.ThanhTien;
                        // Cột ColSTT (ẩn) vẫn giữ giá trị 0

                        // Tính lại tổng tiền trên grid
                        RecalculateTotalServiceCost();

                        // Reset trạng thái sửa
                        rowBeingEdited = null;
                        if (btnThemDV != null) btnThemDV.Text = "Thêm";

                        // Reset ô nhập liệu
                        ClearDichVuControlsForNewEntry();

                        MessageBox.Show("Đã cập nhật dịch vụ trên danh sách.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Lỗi: Không tìm thấy đối tượng dịch vụ tạm thời để cập nhật.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        // Reset trạng thái sửa nếu có lỗi
                        rowBeingEdited = null;
                        if (btnThemDV != null) btnThemDV.Text = "Thêm";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi cập nhật dịch vụ trên danh sách: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // Reset trạng thái sửa nếu có lỗi
                    rowBeingEdited = null;
                    if (btnThemDV != null) btnThemDV.Text = "Thêm";
                }
            }
            else
            {
                // --- CHẾ ĐỘ THÊM MỚI ---
                try
                {
                    ChiTietSuDungDV newChiTiet = new ChiTietSuDungDV
                    {
                        MaKhachHang = currentMaKhachHang.Value,
                        MaDV = selectedDV.MaDV,
                        SoLuong = soLuong,
                        ThanhTien = CalculateThanhTien(selectedDV.DonGia, soLuong),
                        NgaySuDung = DateTime.Now,
                        DichVu = selectedDV, // Gán navigation property
                        STT = 0 // Quan trọng: Đánh dấu là mục mới
                    };

                    // Thêm vào list tạm thời (nếu bạn vẫn dùng list này để lưu cuối cùng)
                    danhSachDvMoiThem.Add(newChiTiet);

                    // Thêm dòng MỚI vào DataGridView
                    int rowIndex = dtgDichVu.Rows.Add(
                        0, // STT hiển thị tạm thời (sẽ được đánh lại)
                        0, // ColSTT (DB) = 0 cho mục mới
                        selectedDV.TenDV,
                        soLuong,
                        selectedDV.DonGia,
                        newChiTiet.ThanhTien
                    );

                    // !!! QUAN TRỌNG: Lưu đối tượng vào Tag của dòng mới thêm !!!
                    if (rowIndex >= 0)
                    {
                        dtgDichVu.Rows[rowIndex].Tag = newChiTiet;
                    }


                    // Tính lại tổng tiền và đánh lại STT hiển thị
                    RecalculateTotalServiceCost();
                    RenumberDtgDichVuSTT();

                    ClearDichVuControlsForNewEntry(); // Reset ô nhập liệu
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi thêm dịch vụ vào danh sách: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }



        private decimal CalculateThanhTien(decimal donGia, decimal soLuong)
        {
            return donGia * soLuong;
        }
        private void btnLuuDV_Click(object sender, EventArgs e)
        {
            if (currentMaKhachHang == null)
            {
                MessageBox.Show("Vui lòng chọn một phòng có khách hàng...", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<ChiTietSuDungDV> cacMucCanLuuMoi = new List<ChiTietSuDungDV>();
            // --- BƯỚC 1: Duyệt grid, thu thập mục mới ---
            foreach (DataGridViewRow row in dtgDichVu.Rows)
            {
                if (row.IsNewRow) continue;
                int stt = 0;
                if (row.Cells["ColSTT"].Value != null && int.TryParse(row.Cells["ColSTT"].Value.ToString(), out stt)) { }

                if (stt == 0) // Chỉ lấy mục mới
                {
                    // ... (Code lấy tenDV, soLuong, thanhTien, tìm dichVu như cũ) ...
                    string tenDV = row.Cells["ColTenDV"].Value?.ToString();
                    decimal soLuong = Convert.ToDecimal(row.Cells["ColSoLuong"].Value ?? 0);
                    decimal thanhTien = Convert.ToDecimal(row.Cells["ColThanhTien"].Value ?? 0);
                    if (string.IsNullOrEmpty(tenDV) || soLuong <= 0) continue;
                    var dichVu = allDichVuList?.FirstOrDefault(dv => dv.TenDV == tenDV) ?? db.DichVus.FirstOrDefault(dv => dv.TenDV == tenDV);
                    if (dichVu == null) { MessageBox.Show($"Không tìm thấy Mã DV cho '{tenDV}'.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

                    cacMucCanLuuMoi.Add(new ChiTietSuDungDV
                    {
                        MaKhachHang = currentMaKhachHang.Value,
                        MaDV = dichVu.MaDV,
                        SoLuong = soLuong,
                        ThanhTien = thanhTien,
                        NgaySuDung = DateTime.Now
                    });
                }
            }

            bool coMucMoi = cacMucCanLuuMoi.Any();

            try
            {
                // --- BƯỚC 2: Lưu các dịch vụ mới (nếu có) ---
                if (coMucMoi)
                {
                    db.ChiTietSuDungDVs.AddRange(cacMucCanLuuMoi);
                    db.SaveChanges(); // Lưu dịch vụ mới vào DB
                }

                // --- BƯỚC 3: TÍNH LẠI TỔNG GIÁ CUỐI CÙNG ---
                decimal giaPhongGoc = 0;
                decimal tongTienTatCaDichVu = 0;
                string soPhongCuaKhach = "";

                // Lấy thông tin khách hàng và phòng
                var khachHang = db.KhachHangs.Find(currentMaKhachHang.Value);
                if (khachHang != null)
                {
                    soPhongCuaKhach = khachHang.SoPhong;
                    var phong = db.Phongs.FirstOrDefault(p => p.SoPhong == soPhongCuaKhach);
                    if (phong != null)
                    {
                        string thoiGianThueKey = "0";
                        int soGioDaThue = phong.SoGioThue ?? 0;
                        if (soGioDaThue > 0 && soGioDaThue <= 1) thoiGianThueKey = "1";
                        else if (soGioDaThue > 1 && soGioDaThue <= 3) thoiGianThueKey = "3";
                        else if (soGioDaThue > 3 && soGioDaThue <= 12) thoiGianThueKey = "12";
                        else if (soGioDaThue > 12) thoiGianThueKey = "24"; // Giả sử > 12h là tính 1 ngày

                        // Nếu là thuê theo ngày (soGio % 24 == 0), tính theo số ngày
                        if (soGioDaThue > 0 && soGioDaThue % 24 == 0)
                        {
                            decimal gia1Ngay = TinhGia(phong.LoaiPhong, "24");
                            giaPhongGoc = gia1Ngay * (soGioDaThue / 24);
                        }
                        else
                        {
                            giaPhongGoc = TinhGia(phong.LoaiPhong, thoiGianThueKey);
                        }
                    }

                    // Tính tổng tiền TẤT CẢ dịch vụ của khách này từ DB (bao gồm cả dịch vụ vừa thêm)
                    tongTienTatCaDichVu = db.ChiTietSuDungDVs
                                            .Where(ct => ct.MaKhachHang == currentMaKhachHang.Value)
                                            .Sum(ct => (decimal?)ct.ThanhTien) ?? 0; // Dùng Sum nullable

                    // Cập nhật GiaThue và Gia bằng tổng mới
                    khachHang.GiaThue = giaPhongGoc + tongTienTatCaDichVu;
                    if (phong != null)
                    {
                        phong.Gia = giaPhongGoc + tongTienTatCaDichVu;
                    }

                    // Lưu thay đổi giá
                    if (db.ChangeTracker.HasChanges()) // Chỉ lưu nếu có thay đổi (ví dụ khi có dịch vụ mới hoặc giá phòng gốc thay đổi)
                    {
                        db.SaveChanges(); // Lưu cập nhật giá
                    }
                }
                else
                {
                    MessageBox.Show("Không tìm thấy khách hàng để cập nhật giá.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return; // Không tiếp tục nếu không tìm thấy khách
                }


                if (coMucMoi)
                {
                    MessageBox.Show($"Đã lưu {cacMucCanLuuMoi.Count} dịch vụ mới. Tổng giá đã được cập nhật.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Không có dịch vụ mới nào được thêm. Tổng giá đã được tính toán lại (nếu cần).", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // --- BƯỚC 4: Tải lại các Grid ---
                LoadDichVuDaSuDung(currentMaKhachHang.Value);
                LoadTraPhongGrid();
                LoadDanhSachKhachHang();
                LoadPhong();

                // --- BƯỚC 5: Reset controls ---
                ClearDichVuControlsForNewEntry();
                // txtTongDV sẽ được cập nhật bởi LoadDichVuDaSuDung
                if (txtSoPDV != null) txtSoPDV.Text = "";
                if (txtTenKH != null) txtTenKH.Text = "";
                currentMaKhachHang = null;
                // SetDichVuControlsEnabled(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Đã xảy ra lỗi: {ex.Message}...", "Lỗi Database", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearDichVuControlsForNewEntry()
        {
            // Giữ lại DataGridView đã tải và txtTongDV nếu nó hiển thị tổng tiền hóa đơn
            // Chỉ xóa các ô nhập cho dịch vụ mới
            if (cbLoaiDV != null) cbLoaiDV.SelectedIndex = -1;
            if (cbDV != null)
            {
                cbDV.DataSource = null; // Xóa danh sách dịch vụ cụ thể
                cbDV.Text = "";
            }
            if (txtDonViTinh != null) txtDonViTinh.Text = "";
            if (txtDonGiaDV != null) txtDonGiaDV.Text = "";
            if (txtSoLuongDV != null) txtSoLuongDV.Text = "1";
            rowBeingEdited = null; // <-- Thêm dòng này
        }

        private void btnXoaDV_Click(object sender, EventArgs e) // Giả sử tên hàm này
        {
            // 1. Kiểm tra xem có dòng nào được chọn không
            if (dtgDichVu.SelectedRows.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một dịch vụ để xóa...", "Chưa chọn dịch vụ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 2. Kiểm tra khách hàng hiện tại
            if (!currentMaKhachHang.HasValue)
            {
                MessageBox.Show("Không xác định được khách hàng hiện tại để thực hiện xóa.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var selectedRows = dtgDichVu.SelectedRows.Cast<DataGridViewRow>().ToList();

            // 3. Phân loại STT cần xóa khỏi DB và dòng cần xóa khỏi grid
            List<int> sttsToDeleteFromDb = new List<int>();
            List<DataGridViewRow> rowsToRemoveFromGrid = new List<DataGridViewRow>();
            decimal tongTienDaGiam = 0; // Tính tổng tiền bị xóa

            foreach (var row in selectedRows)
            {
                if (row.IsNewRow) continue;
                int stt = 0;
                decimal thanhTienRow = 0;

                // Lấy STT
                if (row.Cells["ColSTT"].Value != null && int.TryParse(row.Cells["ColSTT"].Value.ToString(), out stt)) { }

                // Lấy Thành Tiền để tính tổng giảm
                if (row.Cells["ColThanhTien"].Value != null && decimal.TryParse(row.Cells["ColThanhTien"].Value.ToString(), out thanhTienRow))
                {
                    tongTienDaGiam += thanhTienRow; // Cộng dồn tiền bị xóa
                }

                if (stt > 0)
                {
                    sttsToDeleteFromDb.Add(stt);
                }
                else
                {
                    rowsToRemoveFromGrid.Add(row);
                }
            }

            // 4. Xác nhận xóa với người dùng
            string confirmMessage = $"Bạn có chắc chắn muốn xóa {selectedRows.Count} dịch vụ đã chọn không?";
            if (rowsToRemoveFromGrid.Any())
            {
                confirmMessage += $"\n({rowsToRemoveFromGrid.Count} mục là dịch vụ mới chưa lưu)";
            }
            DialogResult confirmResult = MessageBox.Show(confirmMessage, "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirmResult == DialogResult.Yes)
            {
                bool dbChanged = false;
                bool gridChanged = false;

                try
                {
                    // 5a. Xóa các mục đã có trong Database
                    if (sttsToDeleteFromDb.Any())
                    {
                        var entitiesToDelete = db.ChiTietSuDungDVs.Where(ct => sttsToDeleteFromDb.Contains(ct.STT)).ToList();
                        if (entitiesToDelete.Any())
                        {
                            db.ChiTietSuDungDVs.RemoveRange(entitiesToDelete);
                            // Không gọi SaveChanges() ngay, để cập nhật giá rồi lưu 1 lần
                            dbChanged = true;
                        }
                    }

                    // 5b. Xóa các mục mới khỏi DataGridView (chưa lưu DB)
                    if (rowsToRemoveFromGrid.Any())
                    {
                        // Sắp xếp giảm dần theo Index trước khi xóa khỏi grid
                        foreach (var row in rowsToRemoveFromGrid.OrderByDescending(r => r.Index).ToList())
                        {
                            if (!row.IsNewRow && row.Index >= 0 && row.Index < dtgDichVu.Rows.Count)
                            {
                                dtgDichVu.Rows.RemoveAt(row.Index);
                            }
                        }
                        gridChanged = true;
                    }

                    // --- BƯỚC 5c: CẬP NHẬT (TRỪ) GIÁ TRONG KHACHHANG VÀ PHONG ---
                    if ((dbChanged || gridChanged) && tongTienDaGiam > 0)
                    {
                        var khachHang = db.KhachHangs.Find(currentMaKhachHang.Value);
                        if (khachHang != null)
                        {
                            // Trừ tiền đã xóa khỏi GiaThue
                            khachHang.GiaThue = (khachHang.GiaThue ?? 0) - tongTienDaGiam;
                            if (khachHang.GiaThue < 0) khachHang.GiaThue = 0; // Đảm bảo không âm

                            var phong = db.Phongs.FirstOrDefault(p => p.SoPhong == khachHang.SoPhong);
                            if (phong != null)
                            {
                                // Trừ tiền đã xóa khỏi Gia phòng
                                phong.Gia = (phong.Gia ?? 0) - tongTienDaGiam;
                                if (phong.Gia < 0) phong.Gia = 0; // Đảm bảo không âm
                            }
                        }
                    }

                    // --- BƯỚC 5d: LƯU TẤT CẢ THAY ĐỔI (Xóa DV + Cập nhật giá) ---
                    if (db.ChangeTracker.HasChanges()) // Chỉ lưu nếu có thay đổi
                    {
                        db.SaveChanges();
                    }

                    // 6. Cập nhật lại giao diện
                    // Tải lại grid dịch vụ sau khi xóa DB và cập nhật giá
                    if (currentMaKhachHang.HasValue) LoadDichVuDaSuDung(currentMaKhachHang.Value);

                    // Tải lại các grid khác để hiển thị giá mới
                    LoadTraPhongGrid();
                    LoadDanhSachKhachHang();
                    LoadPhong();


                    if (dbChanged || gridChanged)
                    {
                        MessageBox.Show($"Đã xóa thành công {selectedRows.Count} dịch vụ.", "Hoàn thành", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Không có dịch vụ nào được xóa.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Đã xảy ra lỗi khi xóa dịch vụ hoặc cập nhật giá: {ex.Message}...", "Lỗi Database", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    // Tải lại grid để đồng bộ nếu có lỗi
                    if (currentMaKhachHang.HasValue) LoadDichVuDaSuDung(currentMaKhachHang.Value);
                }
            }
        }

        // Hàm mới để tính lại tổng tiền từ DataGridView (cần thiết nếu chỉ xóa trên grid)
        private void RecalculateTotalServiceCost()
        {
            if (dtgDichVu == null || dtgDichVu.Columns.Contains("ColThanhTien") == false) return;

            decimal total = 0;
            foreach (DataGridViewRow row in dtgDichVu.Rows)
            {
                if (!row.IsNewRow && row.Cells["ColThanhTien"].Value != null && row.Cells["ColThanhTien"].Value != DBNull.Value)
                {
                    if (decimal.TryParse(row.Cells["ColThanhTien"].Value.ToString(), out decimal thanhTien))
                    {
                        total += thanhTien;
                    }
                }
            }

            if (txtTongDV != null)
            {
                txtTongDV.Text = total.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) + " VNĐ";
            }
        }

        private void dtgDichVu_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // Reset trạng thái sửa mỗi khi click
            rowBeingEdited = null;
            if (btnThemDV != null) btnThemDV.Text = "Thêm";

            if (e.RowIndex < 0 || e.RowIndex >= dtgDichVu.Rows.Count || dtgDichVu.Rows[e.RowIndex].IsNewRow)
            {
                ClearDichVuControlsForNewEntry(); // Xóa ô nhập nếu click ra ngoài
                return;
            }

            if (Control.ModifierKeys == Keys.None)
            {
                try
                {
                    DataGridViewRow selectedRow = dtgDichVu.Rows[e.RowIndex];
                    string tenDV = selectedRow.Cells["ColTenDV"].Value?.ToString();
                    string soLuongStr = selectedRow.Cells["ColSoLuong"].Value?.ToString();
                    int sttDb = 0; // STT từ database (cột ẩn)
                    var sttCell = selectedRow.Cells["ColSTT"]; // Lấy ô chứa STT DB
                    if (sttCell?.Value != null && sttCell.Value != DBNull.Value)
                    {
                        int.TryParse(sttCell.Value.ToString(), out sttDb);
                    }

                    if (!string.IsNullOrEmpty(tenDV))
                    {
                        // Tìm DichVu gốc
                        DichVu selectedDichVu = allDichVuList?.FirstOrDefault(dv => dv.TenDV == tenDV) ?? db.DichVus.FirstOrDefault(dv => dv.TenDV == tenDV);

                        if (selectedDichVu != null)
                        {
                            // --- Điền dữ liệu vào controls (Tạm ngắt event handler) ---
                            // ... (Code điền cbLoaiDV, LoadDichVuListByType, cbDV, txtDonGiaDV, txtSoLuongDV như trước) ...
                            // Tạm ngắt event handler
                            if (cbLoaiDV != null) cbLoaiDV.SelectedIndexChanged -= CbLoaiDV_SelectedIndexChanged;
                            if (cbDV != null) cbDV.SelectedIndexChanged -= CbDV_SelectedIndexChanged;

                            // Chọn Loại
                            if (cbLoaiDV != null && cbLoaiDV.Items.Contains(selectedDichVu.LoaiDichVu))
                            {
                                cbLoaiDV.SelectedItem = selectedDichVu.LoaiDichVu;
                                LoadDichVuListByType(selectedDichVu.LoaiDichVu); // Nạp lại cbDV
                            }
                            else { /* Reset nếu không thấy loại */ }

                            // Chọn Dịch vụ
                            if (cbDV != null && cbDV.DataSource != null)
                            {
                                cbDV.SelectedValue = selectedDichVu.MaDV;
                                if (cbDV.SelectedValue == null || !cbDV.SelectedValue.Equals(selectedDichVu.MaDV)) { /* Fallback tìm theo text */ }
                            }
                            // Điền giá, số lượng
                            if (txtDonGiaDV != null) txtDonGiaDV.Text = selectedDichVu.DonGia.ToString("N0");
                            if (txtSoLuongDV != null) txtSoLuongDV.Text = soLuongStr ?? "1";

                            // Gắn lại event handler
                            if (cbLoaiDV != null) cbLoaiDV.SelectedIndexChanged += CbLoaiDV_SelectedIndexChanged;
                            if (cbDV != null) cbDV.SelectedIndexChanged += CbDV_SelectedIndexChanged;


                            // --- KIỂM TRA VÀ LƯU TRẠNG THÁI SỬA ---
                            if (sttDb == 0) // Chỉ cho phép sửa nếu là mục CHƯA LƯU (STT DB = 0)
                            {
                                // Lưu lại dòng đang được chọn để sửa
                                rowBeingEdited = selectedRow;
                                if (btnThemDV != null) btnThemDV.Text = "Cập nhật"; // Đổi chữ nút Thêm
                            }
                            // else: Mục đã lưu (sttDb > 0), không cho sửa bằng nút này, nút vẫn là "Thêm"
                        }
                        else { /* ... (Xử lý không tìm thấy DichVu gốc) ... */ }
                    }
                }
                catch (Exception ex) { /* ... (Xử lý lỗi) ... */ }
            }
        }

        private void RenumberDtgDichVuSTT()
        {
            if (dtgDichVu == null || dtgDichVu.Columns.Contains("ColSTTDisplay") == false) return;

            int stt = 1;
            foreach (DataGridViewRow row in dtgDichVu.Rows)
            {
                if (!row.IsNewRow) // Bỏ qua dòng trống cuối cùng nếu có
                {
                    row.Cells["ColSTTDisplay"].Value = stt++;
                }
            }
        }

        private void LoadDichVuListByType(string selectedLoaiDV)
        {
            if (cbDV == null || allDichVuList == null) return;

            var dichVuList = allDichVuList
                              .Where(dv => dv.LoaiDichVu != null && dv.LoaiDichVu.Equals(selectedLoaiDV, StringComparison.OrdinalIgnoreCase) && dv.Active == true)
                              .OrderBy(dv => dv.TenDV)
                              .ToList();

            // Temporarily detach event handler
            cbDV.SelectedIndexChanged -= CbDV_SelectedIndexChanged;

            cbDV.DataSource = dichVuList;
            cbDV.DisplayMember = "TenDV";
            cbDV.ValueMember = "MaDV";
            cbDV.SelectedIndex = -1; // Deselect

            // Reattach event handler
            cbDV.SelectedIndexChanged += CbDV_SelectedIndexChanged;

            // Reset related controls
            if (txtDonGiaDV != null) txtDonGiaDV.Text = "";
            // Skip txtDonViTinh if not used
            if (txtSoLuongDV != null) txtSoLuongDV.Text = "1";
        }

        private void chkDangThue_CheckedChanged(object sender, EventArgs e)
        {
            txtTimKiemKH_TextChanged(txtTimKiemKH, EventArgs.Empty);
        }

        private void printDocument1_PrintPage(object sender, PrintPageEventArgs e)
        {
            int startY = 50;
            int pageBottom = e.PageBounds.Height - 50;

            // 1. Kiểm tra xem có gì để in không
            if (invoicesToPrint == null || invoicePrintIndex >= invoicesToPrint.Count)
            {
                e.HasMorePages = false;
                invoicesToPrint = null; // Dọn dẹp
                invoicePrintIndex = 0;
                return; // Quan trọng: THOÁT ngay
            }

            // 2. Lấy bill hiện tại (sẽ là bill gộp, hoặc bill lẻ)
            var currentBill = invoicesToPrint[invoicePrintIndex];

            // 3. VẼ bill này
            InvoicePrinter.Render(e, currentBill, startY, pageBottom, out bool overflow);

            // (Bạn có thể xử lý 'overflow' ở đây nếu bill quá dài)

            // 4. Tăng biến đếm
            invoicePrintIndex++;

            // 5. Kiểm tra xem còn bill khác trong danh sách không
            if (invoicePrintIndex < invoicesToPrint.Count)
            {
                e.HasMorePages = true; // Vẫn còn -> Gọi lại hàm này cho trang mới
            }
            else
            {
                e.HasMorePages = false; // Hết bill -> Dừng

                // Dọn dẹp
                invoicesToPrint = null;
                invoicePrintIndex = 0;
            }
        }
    }
}
