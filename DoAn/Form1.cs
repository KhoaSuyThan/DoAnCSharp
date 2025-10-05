using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DoAn
{
    public partial class Form1 : Form
    {
        
        private readonly Dictionary<string, string> _accounts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "admin", "123" },      // Admin
            { "le tan", "123" },     // Lễ tân
            { "buong", "123" }       // Nhân viên buồng phòng
        };

        public Form1()
        {
            InitializeComponent();

            // Gán event handlers (nếu bạn chưa double-click trong Designer)
            btnDangNhap.Click += BtnDangNhap_Click;
            btnThoat.Click += BtnThoat_Click;

            // Ẩn mật khẩu (nếu bạn chưa đặt trong Designer)
            txtMatKhau.UseSystemPasswordChar = true;

            // Option: Enter nhấn đăng nhập
            this.AcceptButton = btnDangNhap;
            this.CancelButton = btnThoat;
        }

        private void BtnDangNhap_Click(object sender, EventArgs e)
        {
            string username = txtTaiKhoan.Text.Trim();
            string password = txtMatKhau.Text;

            // Validation cơ bản
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

            // Kiểm tra tài khoản (thí dụ: hardcode)
            if (_accounts.TryGetValue(username, out string expectedPassword) && expectedPassword == password)
            {
                // Đăng nhập thành công
                // Bạn có thể truyền thông tin user/role vào Form2 nếu cần
                var f2 = new Form2(); // đảm bảo Form2 tồn tại trong project
                this.Hide();          // ẩn Form1
                f2.ShowDialog();      // mở Form2 ở chế độ modal
                // Khi Form2 đóng -> đóng ứng dụng (hoặc bạn có thể Show lại Form1 tuỳ logic)
                this.Close();
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
            // Xác nhận trước khi thoát (tùy chọn)
            var r = MessageBox.Show("Bạn có muốn thoát chương trình?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes)
            {
                Application.Exit();
            }
        }
    }
}
