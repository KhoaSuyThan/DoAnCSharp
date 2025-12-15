USE BaiDoAn;
GO

-- ====================================================
-- XÓA RÀNG BUỘC KHÓA NGOẠI CŨ (Nếu có, để tránh lỗi khi xóa bảng)
-- ====================================================
-- (Thêm lệnh xóa FK nếu bạn đã từng tạo chúng trước đây)
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ChiTiet_KhachHang')
    ALTER TABLE ChiTietSuDungDV DROP CONSTRAINT FK_ChiTiet_KhachHang;
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_ChiTiet_DichVu')
    ALTER TABLE ChiTietSuDungDV DROP CONSTRAINT FK_ChiTiet_DichVu;
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_KhachHang_Phong')
    ALTER TABLE KhachHang DROP CONSTRAINT FK_KhachHang_Phong;
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_KhachHang_NhanVien')
    ALTER TABLE KhachHang DROP CONSTRAINT FK_KhachHang_NhanVien;
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Phong_NhanVien')
    ALTER TABLE Phong DROP CONSTRAINT FK_Phong_NhanVien;
GO

-- ====================================================
-- XÓA BẢNG CŨ (Đúng thứ tự)
-- ====================================================
IF OBJECT_ID('ChiTietSuDungDV', 'U') IS NOT NULL DROP TABLE ChiTietSuDungDV;
IF OBJECT_ID('DichVu', 'U') IS NOT NULL DROP TABLE DichVu;
IF OBJECT_ID('KhachHang', 'U') IS NOT NULL DROP TABLE KhachHang;
IF OBJECT_ID('Phong', 'U') IS NOT NULL DROP TABLE Phong; -- Xóa Phong trước NhanVien vì Phong có thể tham chiếu NhanVien
IF OBJECT_ID('NhanVien', 'U') IS NOT NULL DROP TABLE NhanVien;
IF OBJECT_ID('TongDoanhThu', 'U') IS NOT NULL DROP TABLE TongDoanhThu;
GO

-- ====================================================
-- BẢNG NHÂN VIÊN (Giữ STT làm PK, MaNhanVien là UNIQUE)
-- ====================================================
CREATE TABLE NhanVien (
    STT INT PRIMARY KEY IDENTITY(1,1), -- Giữ STT làm PK chính
    MaNhanVien NVARCHAR(20) UNIQUE NOT NULL, -- MaNV là duy nhất để các bảng khác tham chiếu
    TenNhanVien NVARCHAR(100),
    GioiTinh NVARCHAR(10),
    SDT NVARCHAR(15),
    Email NVARCHAR(100),
    ChucVu NVARCHAR(50),
    CaLamViec NVARCHAR(50),
    NgaySinh DATE,
    DiaChi NVARCHAR(200),
    TaiKhoan CHAR(100),
    MatKhau NVARCHAR(255) -- Nên lưu trữ hash mật khẩu an toàn hơn
);
GO

-- Chèn dữ liệu mẫu NhanVien
INSERT INTO NhanVien ( MaNhanVien, TenNhanVien, GioiTinh, SDT, Email, ChucVu, CaLamViec, NgaySinh, DiaChi, TaiKhoan, MatKhau)
VALUES
('NV01', N'Nguyễn Võ Lê Khoa', N'Nam', '0904001122', 'khoanv249@gmail.com', N'Quan ly', N'Sang', '1998-02-10', N'Ha Noi', 'admin','12345'),
('NV02', N'Nguyễn Thành Nhất Nam', N'Nu', '0904001144', 'nguyennhatnam213654@gmail.com', N'Le tan', N'Toi', '1998-02-15', N'Da Nang', 'buong', '12345'),
('NV03', N'Trần Đức Huy', N'Nam', '0905002233', 'duchuy3d2005@gmail.com', N'Le tan', N'Ca ngay', '1990-09-20', N'Sai Gon','letan', '12345');
GO

-- Cập nhật mật khẩu đã HASH
UPDATE NhanVien
SET MatKhau = CONVERT(NVARCHAR(255), HASHBYTES('SHA2_256', '12345'), 2)
WHERE TaiKhoan IN ('admin', 'buong', 'letan');
GO

-- ====================================================
-- BẢNG PHÒNG (Giữ STT làm PK, SoPhong là UNIQUE, Thêm MaNV FK)
-- ====================================================
CREATE TABLE Phong (
    STT INT PRIMARY KEY IDENTITY(1,1),          -- Giữ STT làm PK chính
    SoPhong NVARCHAR(10) NOT NULL UNIQUE,       -- SoPhong là duy nhất để KhachHang tham chiếu
    Tang INT,
    LoaiPhong NVARCHAR(50),
    Gia DECIMAL(18,0), -- Đổi thành DECIMAL(18,0) cho nhất quán
    TinhTrang NVARCHAR(50),
    SoGioThue INT,
    NgayThuePhong DATETIME,
    NgayTraPhong DATETIME,
    MaNV NVARCHAR(20) NULL -- Thêm cột Mã Nhân Viên (Nullable)
    -- Ràng buộc khóa ngoại sẽ được thêm sau khi tạo bảng NhanVien
);
GO

-- Thêm khóa ngoại cho Phong -> NhanVien
ALTER TABLE Phong
ADD CONSTRAINT FK_Phong_NhanVien FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNhanVien);
GO

-- Dữ liệu mẫu cho bảng Phòng (Thêm giá trị NULL cho MaNV)
INSERT INTO Phong (SoPhong, Tang, LoaiPhong, Gia, TinhTrang, SoGioThue, NgayThuePhong, NgayTraPhong, MaNV)
VALUES
('101', 1, 'Don', 0, N'Chua thue', 0, NULL, NULL, NULL),
('102', 1, 'Don', 0, N'Chua thue', 0, NULL, NULL, NULL),
('103', 1, 'Doi', 0, N'Chua thue', 0, NULL, NULL, NULL),
('104', 1, 'Bon', 0, N'Chua thue', 0, NULL, NULL, NULL),
('201', 2, 'Don', 0, N'Chua thue', 0, NULL, NULL, NULL),
('202', 2, 'Don', 0, N'Chua thue', 0, NULL, NULL, NULL),
('203', 2, 'Doi', 0, N'Chua thue', 0, NULL, NULL, NULL),
('204', 2, 'Doi', 0, N'Chua thue', 0, NULL, NULL, NULL),
('205', 2, 'Bon', 0, N'Chua thue', 0, NULL, NULL, NULL),
('206', 2, 'Bon', 0, N'Chua thue', 0, NULL, NULL, NULL),
('301', 3, 'Don', 0, N'Chua thue', 0, NULL, NULL, NULL),
('302', 3, 'Don', 0, N'Chua thue', 0, NULL, NULL, NULL),
('303', 3, 'Doi', 0, N'Chua thue', 0, NULL, NULL, NULL),
('304', 3, 'Doi', 0, N'Chua thue', 0, NULL, NULL, NULL),
('305', 3, 'Bon', 0, N'Chua thue', 0, NULL, NULL, NULL),
('306', 3, 'Bon', 0, N'Chua thue', 0, NULL, NULL, NULL);
GO

-- ====================================================
-- BẢNG KHÁCH HÀNG (Giữ STT làm PK, Thêm SoPhong FK, MaNV FK)
-- ====================================================
CREATE TABLE KhachHang (
    STT INT PRIMARY KEY IDENTITY(1,1),      -- Giữ STT làm PK chính
    TenKhachHang NVARCHAR(100) NOT NULL,
    CCCD VARCHAR(20),                       -- Giữ VARCHAR nếu CCCD chỉ có số
    QuocTich NVARCHAR(50),
    SoPhong NVARCHAR(10) NOT NULL,          -- Đổi sang NVARCHAR cho khớp với Phong.SoPhong
    LoaiPhong NVARCHAR(50),
    GioiTinh NVARCHAR(10),
    NgayThue DATETIME NOT NULL,
    NgayTra DATETIME NULL,
    SDT VARCHAR(15),                        -- Giữ VARCHAR nếu SĐT có thể có ký tự đặc biệt
    GiaThue DECIMAL(18,0),
    DiaChi NVARCHAR(255),
    NgaySinh DATE,
    MaNV NVARCHAR(20) NULL                  -- Thêm cột Mã Nhân Viên (Nullable)
    -- Ràng buộc khóa ngoại sẽ được thêm sau
);
GO

-- Thêm khóa ngoại cho KhachHang -> Phong (qua SoPhong)
ALTER TABLE KhachHang
ADD CONSTRAINT FK_KhachHang_Phong FOREIGN KEY (SoPhong) REFERENCES Phong(SoPhong);
GO

-- Thêm khóa ngoại cho KhachHang -> NhanVien
ALTER TABLE KhachHang
ADD CONSTRAINT FK_KhachHang_NhanVien FOREIGN KEY (MaNV) REFERENCES NhanVien(MaNhanVien);
GO

-- ====================================================
-- BẢNG DICHVU (Giữ nguyên cấu trúc)
-- ====================================================
CREATE TABLE DichVu (
    MaDV INT PRIMARY KEY IDENTITY(1,1),
    TenDV NVARCHAR(100) NOT NULL,
    DonViTinh NVARCHAR(20) NOT NULL,
    DonGia DECIMAL(18,0) NOT NULL,
    LoaiDichVu NVARCHAR(50) NOT NULL,
    Active BIT DEFAULT 1
);
GO

-- Chèn dữ liệu mẫu DichVu (Giữ nguyên)
INSERT INTO DichVu (TenDV, DonViTinh, DonGia, LoaiDichVu) VALUES
('Banh mi', N'Dia', 20000, N'An Sang'), ('Xoi', N'Dia', 20000, N'An Sang'), ('Pho (Sang)', N'Bat', 35000, N'An Sang'),
('Com (Trua)', N'Dia', 35000, N'An Trua'), ('Pho (Trua)', N'Bat', 35000, N'An Trua'), ('Bun bo (Trua)', N'To', 40000, N'An Trua'),
('Com (Toi)', N'Dia', 35000, N'An Toi'), ('Pho (Toi)', N'Bat', 35000, N'An Toi'), ('Bun bo (Toi)', N'To', 40000, N'An Toi'),
('Coffee', N'Ly', 20000, N'Do Uong'), ('Pepsi', N'Lon', 15000, N'Do Uong'), ('Coca', N'Lon', 15000, N'Do Uong'),
('Nuoc loc', N'Chai', 10000, N'Do Uong'), ('Red Bull', N'Lon', 20000, N'Do Uong'), ('Bia', N'Lon', 20000, N'Do Uong'),
(N'Giat Say Quan Ao', N'Kg', 10000, N'Giat Say'), (N'Giat Say Giay Dep', N'Doi', 30000, N'Giat Say'),
(N'Massage', N'Lan', 100000, N'Giai Tri'), (N'Xong hoi', N'Lan', 100000, N'Giai Tri'),
(N'Boi', N'Lan', 30000, N'Giai Tri'), (N'Gym', N'Lan', 30000, N'Giai Tri');
GO

-- ====================================================
-- Bảng ChiTietSuDungDV (Giữ nguyên cấu trúc và FK)
-- ====================================================
CREATE TABLE ChiTietSuDungDV (
    STT INT PRIMARY KEY IDENTITY(1,1),
    MaKhachHang INT NOT NULL, -- FK tới KhachHang.STT
    MaDV INT NOT NULL,        -- FK tới DichVu.MaDV
    SoLuong DECIMAL(10,2) NOT NULL,
    ThanhTien DECIMAL(18,0) NOT NULL,
    NgaySuDung DATETIME DEFAULT GETDATE(),

    -- Ràng buộc khóa ngoại được thêm rõ ràng
    CONSTRAINT FK_ChiTiet_KhachHang FOREIGN KEY (MaKhachHang) REFERENCES KhachHang(STT),
    CONSTRAINT FK_ChiTiet_DichVu FOREIGN KEY (MaDV) REFERENCES DichVu(MaDV)
);
GO

-- ====================================================
-- BẢNG TONGDOANHTHU (Giữ nguyên cấu trúc cũ)
-- ====================================================
CREATE TABLE TongDoanhThu (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    TongTien DECIMAL(18,0) NOT NULL DEFAULT 0, -- Đổi thành DECIMAL(18,0)
    NgayCapNhat DATETIME NOT NULL DEFAULT GETDATE()
);
GO
-- Chèn một dòng dữ liệu ban đầu cho TongDoanhThu nếu chưa có
INSERT INTO TongDoanhThu (TongTien, NgayCapNhat) VALUES (0, GETDATE());
GO

-- ====================================================
-- KIỂM TRA DỮ LIỆU TẤT CẢ CÁC BẢNG
-- ====================================================
PRINT 'Kiem tra bang NhanVien:'; SELECT * FROM NhanVien;
PRINT 'Kiem tra bang Phong:'; SELECT * FROM Phong;
PRINT 'Kiem tra bang KhachHang:'; SELECT * FROM KhachHang;
PRINT 'Kiem tra bang DichVu:'; SELECT * FROM DichVu;
PRINT 'Kiem tra bang ChiTietSuDungDV:'; SELECT * FROM ChiTietSuDungDV;
PRINT 'Kiem tra bang TongDoanhThu:'; SELECT * FROM TongDoanhThu;
GO