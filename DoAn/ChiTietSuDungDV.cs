namespace DoAn
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("ChiTietSuDungDV")] // Tên bảng trong CSDL
    public partial class ChiTietSuDungDV
    {
        [Key] // Khóa chính
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Tự động tăng
        public int STT { get; set; }

        public int MaKhachHang { get; set; }

        public int MaDV { get; set; }

        public decimal SoLuong { get; set; }

        public decimal ThanhTien { get; set; }

        public DateTime? NgaySuDung { get; set; } // Dùng DateTime? cho phép cột DEFAULT
        [ForeignKey("MaKhachHang")]
        // Quan hệ: Một Chi Tiết thuộc về một Khách Hàng
        public virtual KhachHang KhachHang { get; set; }

        // Quan hệ: Một Chi Tiết thuộc về một Dịch Vụ
        public virtual DichVu DichVu { get; set; }
    }
}