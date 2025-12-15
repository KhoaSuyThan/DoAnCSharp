using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace DoAn
{
    public partial class Form1 : Form
    {
        // Kết nối Entity Framework
        private Model1 db = new Model1();

        public Form1()
        {
            InitializeComponent();

            btnDangNhap.Click += BtnDangNhap_Click;
            btnThoat.Click += BtnThoat_Click;

            txtMatKhau.UseSystemPasswordChar = true;
            this.AcceptButton = btnDangNhap;
            this.CancelButton = btnThoat;

            txtTaiKhoan.KeyDown += TxtTaiKhoan_KeyDown;
            txtMatKhau.KeyDown += TxtMatKhau_KeyDown;
        }

        

        // --- THÊM HÀM BĂM MẬT KHẨU TRẢ VỀ CHUỖI HEX ---
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                // Băm mật khẩu (chuỗi) thành mảng byte
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));

                // Chuyển mảng byte sang chuỗi Hex (64 ký tự)
                var sb = new StringBuilder();
                foreach (byte b in bytes)
                {
                    // "X2" định dạng hai chữ số thập lục phân (Hex), viết HOA
                    sb.Append(b.ToString("X2"));
                }
                // Trả về chuỗi băm
                return sb.ToString();
            }
        }
        // ---------------------------------------------------


        private void BtnDangNhap_Click(object sender, EventArgs e)
        {
            string username = txtTaiKhoan.Text.Trim();
            string password = txtMatKhau.Text;

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Vui lòng nhập tên tài khoản.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTaiKhoan.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vui lòng nhập mật khẩu.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtMatKhau.Focus();
                return;
            }

            // 1. Băm mật khẩu người dùng nhập thành chuỗi Hex
            // Chuỗi này phải khớp với giá trị đã lưu trong cột MatKhau (NVARCHAR) của CSDL
            string hashedPassword = HashPassword(password);

            // 2. 🔍 Kiểm tra trong database (bảng NhanVien)
            // So sánh TaiKhoan và MatKhau (chuỗi băm)
            var user = db.NhanViens.FirstOrDefault(
                nv => nv.TaiKhoan == username && nv.MatKhau == hashedPassword
            );

            if (user != null)
            {
                // Đăng nhập thành công
                MessageBox.Show($"Đăng nhập thành công! Xin chào {user.TenNhanVien} ({user.ChucVu})",
                    "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

                var f2 = new Form2();

                // Gửi thông tin người dùng sang Form2 (nếu cần)
                f2.Owner = this;
                f2.Tag = user;

                f2.FormClosed += (s, args) =>
                {
                    if (!this.IsDisposed)
                    {
                        this.Show();
                        txtTaiKhoan.Clear();
                        txtMatKhau.Clear();
                        txtTaiKhoan.Focus();
                    }
                };

                this.Hide();
                f2.Show();
            }
            else
            {
                MessageBox.Show("Tên tài khoản hoặc mật khẩu không đúng.", "Đăng nhập thất bại", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtMatKhau.Clear();
                txtMatKhau.Focus();
            }
        }

        private void BtnThoat_Click(object sender, EventArgs e)
        {
            var r = MessageBox.Show("Bạn có muốn thoát chương trình?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        private void TxtTaiKhoan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                txtMatKhau.Focus();
                e.Handled = true;
            }
        }

        private void TxtMatKhau_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
            {
                txtTaiKhoan.Focus();
                e.Handled = true;
            }
        }

        private void llQuenMK_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.Hide(); // Tạm ẩn Form đăng nhập
            Form3 f3 = new Form3();
            f3.ShowDialog(); // Mở Form quên mật khẩu và đợi nó đóng
            this.Show(); // Hiện lại Form đăng nhập sau khi Form3 đóng
        }
    }
}
