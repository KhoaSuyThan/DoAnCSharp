using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DoAn
{
    public partial class Form3 : Form
    {
        private Model1 db = new Model1();
        private string maXacNhan; // Biến để lưu mã xác nhận đã gửi
        private NhanVien nguoiDungCanDoiMatKhau; // Lưu nhân viên cần đổi mật khẩu

        public Form3()
        {
            InitializeComponent();
        }

        private void Form3_Load(object sender, EventArgs e)
        {
            // --- Thiết lập trạng thái ban đầu cho các control ---
            txtMaXacNhan.Enabled = false;
            txtMKMoi.Enabled = false;
            txtXacNhanMK.Enabled = false;
            btnXacNhan.Enabled = false; // Nút xác nhận chỉ bật khi có mã gửi đi

            // --- Thiết lập để che mật khẩu ---
            txtMKMoi.UseSystemPasswordChar = true;
            txtXacNhanMK.UseSystemPasswordChar = true;

            // --- Gán sự kiện cho các button ---
            btnGui.Click += BtnGui_Click;
            btnXacNhan.Click += BtnXacNhan_Click;

            txtEmail.KeyDown += TxtEmail_KeyDown;
            txtMaXacNhan.KeyDown += TxtMaXacNhan_KeyDown;
            txtMKMoi.KeyDown += TxtMKMoi_KeyDown;
            txtXacNhanMK.KeyDown += TxtXacNhanMK_KeyDown;
        }

        // Thêm "async" vào đây
        private async void BtnGui_Click(object sender, EventArgs e)
        {
            string email = txtEmail.Text.Trim();

            if (string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Vui lòng nhập email của bạn.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            nguoiDungCanDoiMatKhau = db.NhanViens.FirstOrDefault(nv => nv.Email == email);

            if (nguoiDungCanDoiMatKhau == null)
            {
                MessageBox.Show("Email không tồn tại trong hệ thống. Vui lòng kiểm tra lại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // --- Thêm phản hồi cho người dùng ---
            btnGui.Enabled = false;
            btnGui.Text = "Đang gửi...";
            this.Cursor = Cursors.WaitCursor; // Biểu tượng con trỏ chờ

            try
            {
                Random rand = new Random();
                maXacNhan = rand.Next(100000, 999999).ToString();

                // Gửi email bất đồng bộ
                // Thêm "await" và đổi tên hàm (xem bước 2)
                await SendConfirmationEmailAsync(email, maXacNhan);

                MessageBox.Show("Mã xác nhận đã được gửi đến email của bạn. Vui lòng kiểm tra hộp thư.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Kích hoạt các ô cần thiết
                txtMaXacNhan.Enabled = true;
                btnXacNhan.Enabled = true;
                txtEmail.Enabled = false; // Đã gửi xong thì khóa email
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi gửi email: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnGui.Enabled = true; // Nếu lỗi thì mở lại nút Gửi
                btnGui.Text = "Gửi";
            }
            finally
            {
                // Luôn trả lại con trỏ và text nút Gửi (nếu nó vẫn bị khóa)
                this.Cursor = Cursors.Default;
                if (!btnGui.Enabled)
                {
                    btnGui.Text = "Gửi";
                }
            }
        }

        private void BtnXacNhan_Click(object sender, EventArgs e)
        {
            // Giai đoạn 1: Xác thực mã code
            if (txtMKMoi.Enabled == false)
            {
                if (txtMaXacNhan.Text == maXacNhan)
                {
                    MessageBox.Show("Xác nhận mã thành công. Vui lòng nhập mật khẩu mới.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Mở khóa các ô nhập mật khẩu mới
                    txtMKMoi.Enabled = true;
                    txtXacNhanMK.Enabled = true;

                    // Khóa ô nhập mã để không sửa lại
                    txtMaXacNhan.Enabled = false;
                }
                else
                {
                    MessageBox.Show("Mã xác nhận không đúng. Vui lòng thử lại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return; // Dừng lại để người dùng nhập mật khẩu
            }

            // Giai đoạn 2: Đổi mật khẩu
            string matKhauMoi = txtMKMoi.Text;
            string xacNhanMatKhau = txtXacNhanMK.Text;

            if (string.IsNullOrEmpty(matKhauMoi) || string.IsNullOrEmpty(xacNhanMatKhau))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ mật khẩu mới và xác nhận mật khẩu.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (matKhauMoi != xacNhanMatKhau)
            {
                MessageBox.Show("Mật khẩu xác nhận không khớp. Vui lòng nhập lại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Cập nhật mật khẩu mới vào database
            try
            {
                // **QUAN TRỌNG**: Mã hóa mật khẩu trước khi lưu
                nguoiDungCanDoiMatKhau.MatKhau = HashPassword(matKhauMoi);
                db.SaveChanges(); // Lưu thay đổi

                MessageBox.Show("Đổi mật khẩu thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close(); // Đóng form sau khi thành công
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi cập nhật mật khẩu: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Đổi tên hàm, dùng "async Task"
        private async Task SendConfirmationEmailAsync(string toEmail, string code)
        {
            string fromEmail = "khoanv249@gmail.com";
            string fromPassword = "htuu ijkb jffg epip";

            // Bọc trong 'using' để tự động giải phóng tài nguyên
            using (SmtpClient client = new SmtpClient("smtp.gmail.com"))
            {
                client.Port = 587;
                client.Credentials = new NetworkCredential(fromEmail, fromPassword);
                client.EnableSsl = true;

                using (MailMessage mailMessage = new MailMessage())
                {
                    mailMessage.From = new MailAddress(fromEmail);
                    mailMessage.To.Add(toEmail); // Thêm người nhận
                    mailMessage.Subject = "Mã xác nhận đặt lại mật khẩu";
                    mailMessage.Body = $"Xin chào {nguoiDungCanDoiMatKhau.TenNhanVien},\n\nMã xác nhận của bạn là: {code}\n\nVui lòng không chia sẻ mã này cho người khác.\n\nTrân trọng.";
                    mailMessage.IsBodyHtml = false;

                    // --- THAY ĐỔI QUAN TRỌNG ---
                    // Dùng SendMailAsync thay vì Send
                    await client.SendMailAsync(mailMessage);
                    // --------------------------
                }
            }
        }   

        // Hàm băm mật khẩu (giống như trong Form1)
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

        private void btnThoat_Click(object sender, EventArgs e)
        {
            var r = MessageBox.Show("Bạn có muốn thoát chương trình?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes)
            {
                this.Close();
            }
        }

        private void TxtEmail_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                // Khi ở txtEmail, nhấn Xuống -> nhảy tới txtMaXacNhan
                txtMaXacNhan.Focus();
                e.Handled = true; // Ngăn con trỏ di chuyển trong textbox
            }
            else if (e.KeyCode == Keys.Up)
            {
                // Khi ở txtEmail, nhấn Lên -> nhảy vòng xuống cuối (txtXacNhanMK)
                txtXacNhanMK.Focus();
                e.Handled = true;
            }
        }

        private void TxtMaXacNhan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                // Khi ở txtMaXacNhan, nhấn Xuống -> nhảy tới txtMKMoi
                txtMKMoi.Focus();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                // Khi ở txtMaXacNhan, nhấn Lên -> nhảy tới txtEmail
                txtEmail.Focus();
                e.Handled = true;
            }
        }

        private void TxtMKMoi_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                // Khi ở txtMKMoi, nhấn Xuống -> nhảy tới txtXacNhanMK
                txtXacNhanMK.Focus();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                // Khi ở txtMKMoi, nhấn Lên -> nhảy tới txtMaXacNhan
                txtMaXacNhan.Focus();
                e.Handled = true;
            }
        }

        private void TxtXacNhanMK_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                // Khi ở txtXacNhanMK, nhấn Xuống -> nhảy vòng lên đầu (txtEmail)
                txtEmail.Focus();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                // Khi ở txtXacNhanMK, nhấn Lên -> nhảy tới txtMKMoi
                txtMKMoi.Focus();
                e.Handled = true;
            }
        }
    }
}