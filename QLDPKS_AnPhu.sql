/*
    QLKS_AnPhu - schema SQL Server sạch, đồng bộ nghiệp vụ khách sạn.
    Dùng cho database mới. Nếu database đang có dữ liệu thật, hãy backup trước.
*/

CREATE DATABASE QLDPKS_AnPhu;
GO

USE QLDPKS_AnPhu;
GO

CREATE TABLE dbo.CALAM
(
    MaCa int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CALAM PRIMARY KEY,
    TenCa nvarchar(50) NOT NULL,
    GioBatDau time(0) NOT NULL,
    GioKetThuc time(0) NOT NULL,
    GhiChu nvarchar(255) NULL
);
GO

CREATE TABLE dbo.NHANVIEN
(
    MaNV int IDENTITY(1,1) NOT NULL CONSTRAINT PK_NHANVIEN PRIMARY KEY,
    HoTen nvarchar(100) NOT NULL,
    GioiTinh nvarchar(10) NULL,
    NgaySinh date NULL,
    SDT varchar(15) NULL,
    DiaChi nvarchar(255) NULL,
    ChucVu nvarchar(50) NOT NULL,
    TrangThai bit NOT NULL CONSTRAINT DF_NHANVIEN_TrangThai DEFAULT (1),
    CONSTRAINT CK_NHANVIEN_GioiTinh CHECK (GioiTinh IS NULL OR GioiTinh IN (N'Nam', N'Nữ', N'Khác')),
    CONSTRAINT CK_NHANVIEN_ChucVu CHECK (ChucVu IN (N'Quản lý', N'Lễ tân', N'Nhân viên'))
);
GO

CREATE TABLE dbo.TAIKHOAN
(
    MaTK int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TAIKHOAN PRIMARY KEY,
    TenDangNhap nvarchar(50) NOT NULL,
    MatKhau nvarchar(255) NOT NULL,
    VaiTro nvarchar(30) NOT NULL,
    TrangThai bit NOT NULL CONSTRAINT DF_TAIKHOAN_TrangThai DEFAULT (1),
    MaNV int NOT NULL,
    CONSTRAINT UQ_TAIKHOAN_TenDangNhap UNIQUE (TenDangNhap),
    CONSTRAINT UQ_TAIKHOAN_MaNV UNIQUE (MaNV),
    CONSTRAINT FK_TAIKHOAN_NHANVIEN FOREIGN KEY (MaNV) REFERENCES dbo.NHANVIEN(MaNV),
    CONSTRAINT CK_TAIKHOAN_VaiTro CHECK (VaiTro IN (N'Quản lý', N'Nhân viên'))
);
GO

CREATE TABLE dbo.PHANQUYENTAIKHOAN
(
    MaTK int NOT NULL,
    MaChucNang nvarchar(50) NOT NULL,
    CONSTRAINT PK_PHANQUYENTAIKHOAN PRIMARY KEY (MaTK, MaChucNang),
    CONSTRAINT FK_PHANQUYENTAIKHOAN_TAIKHOAN FOREIGN KEY (MaTK) REFERENCES dbo.TAIKHOAN(MaTK) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.PHANCONGCA
(
    MaPhanCong int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PHANCONGCA PRIMARY KEY,
    MaNV int NOT NULL,
    MaCa int NOT NULL,
    NgayLam date NOT NULL,
    TrangThai nvarchar(30) NOT NULL CONSTRAINT DF_PHANCONGCA_TrangThai DEFAULT (N'Đã phân công'),
    CONSTRAINT FK_PHANCONGCA_NHANVIEN FOREIGN KEY (MaNV) REFERENCES dbo.NHANVIEN(MaNV),
    CONSTRAINT FK_PHANCONGCA_CALAM FOREIGN KEY (MaCa) REFERENCES dbo.CALAM(MaCa),
    CONSTRAINT UQ_PHANCONGCA_NV_CA_NGAY UNIQUE (MaNV, MaCa, NgayLam)
);
GO

CREATE TABLE dbo.KHACHHANG
(
    MaKH int IDENTITY(1,1) NOT NULL CONSTRAINT PK_KHACHHANG PRIMARY KEY,
    HoTen nvarchar(100) NOT NULL,
    GioiTinh nvarchar(10) NULL,
    NgaySinh date NULL,
    CCCD varchar(20) NULL,
    SDT varchar(15) NULL,
    Email nvarchar(100) NULL,
    DiaChi nvarchar(255) NULL,
    LoaiKhach nvarchar(20) NOT NULL CONSTRAINT DF_KHACHHANG_LoaiKhach DEFAULT (N'Thường'),
    GhiChu nvarchar(500) NULL,
    PhanTramGiamGia decimal(5,2) NOT NULL CONSTRAINT DF_KHACHHANG_GiamGia DEFAULT (0),
    TrangThai nvarchar(30) NOT NULL CONSTRAINT DF_KHACHHANG_TrangThai DEFAULT (N'Đang hoạt động'),
    CONSTRAINT CK_KHACHHANG_GioiTinh CHECK (GioiTinh IS NULL OR GioiTinh IN (N'Nam', N'Nữ', N'Khác')),
    CONSTRAINT CK_KHACHHANG_LoaiKhach CHECK (LoaiKhach IN (N'Thường', N'VIP')),
    CONSTRAINT CK_KHACHHANG_GiamGia CHECK (PhanTramGiamGia BETWEEN 0 AND 100)
);
GO

CREATE UNIQUE INDEX UX_KHACHHANG_CCCD_NotNull
ON dbo.KHACHHANG(CCCD)
WHERE CCCD IS NOT NULL;
GO

CREATE TABLE dbo.LOAIPHONG
(
    MaLoaiPhong int IDENTITY(1,1) NOT NULL CONSTRAINT PK_LOAIPHONG PRIMARY KEY,
    TenLoaiPhong nvarchar(80) NOT NULL,
    SoNguoiToiDa int NOT NULL,
    DonGiaGio decimal(18,2) NOT NULL,
    DonGiaNgay decimal(18,2) NOT NULL,
    DonGiaDem decimal(18,2) NOT NULL,
    TienCocGoiY decimal(18,2) NOT NULL CONSTRAINT DF_LOAIPHONG_TienCoc DEFAULT (0),
    MoTa nvarchar(500) NULL,
    CONSTRAINT UQ_LOAIPHONG_Ten UNIQUE (TenLoaiPhong),
    CONSTRAINT CK_LOAIPHONG_SoNguoi CHECK (SoNguoiToiDa > 0),
    CONSTRAINT CK_LOAIPHONG_Gia CHECK (DonGiaGio >= 0 AND DonGiaNgay >= 0 AND DonGiaDem >= 0 AND TienCocGoiY >= 0)
);
GO

CREATE TABLE dbo.PHONG
(
    MaPhong int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PHONG PRIMARY KEY,
    TenPhong nvarchar(20) NOT NULL,
    SoPhong AS TenPhong PERSISTED,
    Tang int NOT NULL,
    MaLoaiPhong int NOT NULL,
    TrangThai nvarchar(20) NOT NULL CONSTRAINT DF_PHONG_TrangThai DEFAULT (N'Trống'),
    GhiChu nvarchar(500) NULL,
    CONSTRAINT UQ_PHONG_TenPhong UNIQUE (TenPhong),
    CONSTRAINT FK_PHONG_LOAIPHONG FOREIGN KEY (MaLoaiPhong) REFERENCES dbo.LOAIPHONG(MaLoaiPhong),
    CONSTRAINT CK_PHONG_TrangThai CHECK (TrangThai IN (N'Trống', N'Đã đặt', N'Đang thuê', N'Chưa dọn dẹp', N'Bảo trì'))
);
GO

CREATE TABLE dbo.DOANKHACH
(
    MaDoan int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DOANKHACH PRIMARY KEY,
    TenDoan nvarchar(150) NOT NULL,
    MaKhachDaiDien int NULL,
    SoNguoi int NOT NULL CONSTRAINT DF_DOANKHACH_SoNguoi DEFAULT (1),
    GhiChu nvarchar(500) NULL,
    CONSTRAINT FK_DOANKHACH_KHACHHANG FOREIGN KEY (MaKhachDaiDien) REFERENCES dbo.KHACHHANG(MaKH),
    CONSTRAINT CK_DOANKHACH_SoNguoi CHECK (SoNguoi > 0)
);
GO

CREATE TABLE dbo.DATPHONG
(
    MaDatPhong int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DATPHONG PRIMARY KEY,
    MaKH int NOT NULL,
    MaNV int NOT NULL,
    MaPhong int NULL,
    MaDoan int NULL,
    LoaiDat nvarchar(30) NOT NULL CONSTRAINT DF_DATPHONG_LoaiDat DEFAULT (N'Đặt trước'),
    LoaiDatPhong nvarchar(30) NULL,
    NgayDat datetime2(0) NOT NULL CONSTRAINT DF_DATPHONG_NgayDat DEFAULT (SYSDATETIME()),
    NgayNhanDuKien datetime2(0) NOT NULL,
    NgayTraDuKien datetime2(0) NOT NULL,
    TienCoc decimal(18,2) NOT NULL CONSTRAINT DF_DATPHONG_TienCoc DEFAULT (0),
    DatCoc AS TienCoc PERSISTED,
    TrangThai nvarchar(20) NOT NULL CONSTRAINT DF_DATPHONG_TrangThai DEFAULT (N'Đã đặt'),
    SoNguoi int NOT NULL CONSTRAINT DF_DATPHONG_SoNguoi DEFAULT (1),
    GhiChu nvarchar(1000) NULL,
    CONSTRAINT FK_DATPHONG_KHACHHANG FOREIGN KEY (MaKH) REFERENCES dbo.KHACHHANG(MaKH),
    CONSTRAINT FK_DATPHONG_NHANVIEN FOREIGN KEY (MaNV) REFERENCES dbo.NHANVIEN(MaNV),
    CONSTRAINT FK_DATPHONG_PHONG FOREIGN KEY (MaPhong) REFERENCES dbo.PHONG(MaPhong),
    CONSTRAINT FK_DATPHONG_DOANKHACH FOREIGN KEY (MaDoan) REFERENCES dbo.DOANKHACH(MaDoan),
    CONSTRAINT CK_DATPHONG_TrangThai CHECK (TrangThai IN (N'Đã đặt', N'Đã check-in', N'Đã hủy')),
    CONSTRAINT CK_DATPHONG_LoaiDat CHECK (LoaiDat IN (N'Đặt trước', N'Nhận ngay', N'Walk-in')),
    CONSTRAINT CK_DATPHONG_Ngay CHECK (NgayTraDuKien > NgayNhanDuKien),
    CONSTRAINT CK_DATPHONG_Tien CHECK (TienCoc >= 0 AND SoNguoi > 0)
);
GO

CREATE TABLE dbo.CHITIETDATPHONG
(
    MaCTDP int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CHITIETDATPHONG PRIMARY KEY,
    MaDatPhong int NOT NULL,
    MaPhong int NOT NULL,
    NgayNhanDuKien datetime2(0) NULL,
    NgayTraDuKien datetime2(0) NULL,
    DonGia decimal(18,2) NOT NULL CONSTRAINT DF_CHITIETDATPHONG_DonGia DEFAULT (0),
    GhiChu nvarchar(1000) NULL,
    CONSTRAINT FK_CTDP_DATPHONG FOREIGN KEY (MaDatPhong) REFERENCES dbo.DATPHONG(MaDatPhong) ON DELETE CASCADE,
    CONSTRAINT FK_CTDP_PHONG FOREIGN KEY (MaPhong) REFERENCES dbo.PHONG(MaPhong),
    CONSTRAINT UQ_CTDP_DatPhong_Phong UNIQUE (MaDatPhong, MaPhong),
    CONSTRAINT CK_CTDP_DonGia CHECK (DonGia >= 0),
    CONSTRAINT CK_CTDP_Ngay CHECK (NgayNhanDuKien IS NULL OR NgayTraDuKien IS NULL OR NgayTraDuKien > NgayNhanDuKien)
);
GO

CREATE TABLE dbo.PHIEUTHUE
(
    MaThue int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PHIEUTHUE PRIMARY KEY,
    MaDatPhong int NULL,
    MaKH int NOT NULL,
    MaNV int NOT NULL,
    MaPhong int NOT NULL,
    MaDoan int NULL,
    NgayNhan datetime2(0) NOT NULL,
    NgayTraDuKien datetime2(0) NOT NULL,
    NgayTraPhong datetime2(0) NULL,
    SoNguoi int NOT NULL CONSTRAINT DF_PHIEUTHUE_SoNguoi DEFAULT (1),
    TienCoc decimal(18,2) NOT NULL CONSTRAINT DF_PHIEUTHUE_TienCoc DEFAULT (0),
    PhuPhiNhanSom decimal(18,2) NOT NULL CONSTRAINT DF_PHIEUTHUE_PhuPhiNhanSom DEFAULT (0),
    TrangThai nvarchar(20) NOT NULL CONSTRAINT DF_PHIEUTHUE_TrangThai DEFAULT (N'Đang thuê'),
    GhiChu nvarchar(1000) NULL,
    CONSTRAINT FK_PHIEUTHUE_DATPHONG FOREIGN KEY (MaDatPhong) REFERENCES dbo.DATPHONG(MaDatPhong),
    CONSTRAINT FK_PHIEUTHUE_KHACHHANG FOREIGN KEY (MaKH) REFERENCES dbo.KHACHHANG(MaKH),
    CONSTRAINT FK_PHIEUTHUE_NHANVIEN FOREIGN KEY (MaNV) REFERENCES dbo.NHANVIEN(MaNV),
    CONSTRAINT FK_PHIEUTHUE_PHONG FOREIGN KEY (MaPhong) REFERENCES dbo.PHONG(MaPhong),
    CONSTRAINT FK_PHIEUTHUE_DOANKHACH FOREIGN KEY (MaDoan) REFERENCES dbo.DOANKHACH(MaDoan),
    CONSTRAINT CK_PHIEUTHUE_TrangThai CHECK (TrangThai IN (N'Đang thuê', N'Đã trả', N'Đã hủy')),
    CONSTRAINT CK_PHIEUTHUE_Ngay CHECK (NgayTraDuKien > NgayNhan AND (NgayTraPhong IS NULL OR NgayTraPhong >= NgayNhan)),
    CONSTRAINT CK_PHIEUTHUE_Tien CHECK (SoNguoi > 0 AND TienCoc >= 0 AND PhuPhiNhanSom >= 0)
);
GO

CREATE TABLE dbo.DICHVUVATTU
(
    MaDVVT int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DICHVUVATTU PRIMARY KEY,
    TenDVVT nvarchar(100) NOT NULL,
    Loai nvarchar(20) NOT NULL,
    DonGia decimal(18,2) NOT NULL,
    DonViTinh nvarchar(30) NULL,
    SoLuongTon decimal(18,2) NOT NULL CONSTRAINT DF_DVVT_Ton DEFAULT (0),
    TonToiThieu decimal(18,2) NOT NULL CONSTRAINT DF_DVVT_TonToiThieu DEFAULT (0),
    TrangThai bit NOT NULL CONSTRAINT DF_DVVT_TrangThai DEFAULT (1),
    GhiChu nvarchar(500) NULL,
    CONSTRAINT UQ_DVVT_Ten UNIQUE (TenDVVT),
    CONSTRAINT CK_DVVT_Loai CHECK (Loai IN (N'Dịch vụ', N'Vật tư')),
    CONSTRAINT CK_DVVT_TienTon CHECK (DonGia >= 0 AND SoLuongTon >= 0 AND TonToiThieu >= 0)
);
GO

CREATE TABLE dbo.CHITIETPHATSINH
(
    MaCTPS int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CHITIETPHATSINH PRIMARY KEY,
    MaThue int NULL,
    MaDatPhong int NULL,
    MaPhong int NULL,
    MaDVVT int NULL,
    MaNV int NOT NULL,
    SoLuong decimal(18,2) NOT NULL,
    DonGia decimal(18,2) NOT NULL,
    ThanhTien AS CONVERT(decimal(18,2), SoLuong * DonGia) PERSISTED,
    ThoiDiemSuDung datetime2(0) NOT NULL CONSTRAINT DF_CTPS_ThoiDiem DEFAULT (SYSDATETIME()),
    GhiChu nvarchar(500) NULL,
    TrangThai bit NOT NULL CONSTRAINT DF_CTPS_TrangThai DEFAULT (1),
    CONSTRAINT FK_CTPS_PHIEUTHUE FOREIGN KEY (MaThue) REFERENCES dbo.PHIEUTHUE(MaThue),
    CONSTRAINT FK_CTPS_DATPHONG FOREIGN KEY (MaDatPhong) REFERENCES dbo.DATPHONG(MaDatPhong),
    CONSTRAINT FK_CTPS_PHONG FOREIGN KEY (MaPhong) REFERENCES dbo.PHONG(MaPhong),
    CONSTRAINT FK_CTPS_DVVT FOREIGN KEY (MaDVVT) REFERENCES dbo.DICHVUVATTU(MaDVVT),
    CONSTRAINT FK_CTPS_NHANVIEN FOREIGN KEY (MaNV) REFERENCES dbo.NHANVIEN(MaNV),
    CONSTRAINT CK_CTPS_Tien CHECK (SoLuong > 0 AND DonGia >= 0),
    CONSTRAINT CK_CTPS_LienKet CHECK (MaThue IS NOT NULL OR MaDatPhong IS NOT NULL OR MaPhong IS NOT NULL)
);
GO

CREATE TABLE dbo.PHUTHU
(
    MaPhuThu int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PHUTHU PRIMARY KEY,
    TenPhuThu nvarchar(100) NOT NULL,
    SoTienCoDinh decimal(18,2) NULL,
    TyLe decimal(5,2) NULL,
    GhiChu nvarchar(500) NULL,
    CONSTRAINT CK_PHUTHU_Tien CHECK ((SoTienCoDinh IS NULL OR SoTienCoDinh >= 0) AND (TyLe IS NULL OR TyLe >= 0))
);
GO

CREATE TABLE dbo.CHITIETPHUTHU
(
    MaCTPT int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CHITIETPHUTHU PRIMARY KEY,
    MaThue int NOT NULL,
    MaPhuThu int NULL,
    NoiDung nvarchar(200) NOT NULL,
    SoTien decimal(18,2) NOT NULL,
    ThoiDiem datetime2(0) NOT NULL CONSTRAINT DF_CTPT_ThoiDiem DEFAULT (SYSDATETIME()),
    GhiChu nvarchar(500) NULL,
    CONSTRAINT FK_CTPT_PHIEUTHUE FOREIGN KEY (MaThue) REFERENCES dbo.PHIEUTHUE(MaThue),
    CONSTRAINT FK_CTPT_PHUTHU FOREIGN KEY (MaPhuThu) REFERENCES dbo.PHUTHU(MaPhuThu),
    CONSTRAINT CK_CTPT_SoTien CHECK (SoTien >= 0)
);
GO

CREATE TABLE dbo.DOIPHONG
(
    MaDoiPhong int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DOIPHONG PRIMARY KEY,
    MaThue int NOT NULL,
    MaPhongCu int NOT NULL,
    MaPhongMoi int NOT NULL,
    ThoiDiemDoi datetime2(0) NOT NULL CONSTRAINT DF_DOIPHONG_ThoiDiem DEFAULT (SYSDATETIME()),
    ChenhLechTien decimal(18,2) NOT NULL CONSTRAINT DF_DOIPHONG_ChenhLech DEFAULT (0),
    GhiChu nvarchar(500) NULL,
    CONSTRAINT FK_DOIPHONG_PHIEUTHUE FOREIGN KEY (MaThue) REFERENCES dbo.PHIEUTHUE(MaThue),
    CONSTRAINT FK_DOIPHONG_PHONGCU FOREIGN KEY (MaPhongCu) REFERENCES dbo.PHONG(MaPhong),
    CONSTRAINT FK_DOIPHONG_PHONGMOI FOREIGN KEY (MaPhongMoi) REFERENCES dbo.PHONG(MaPhong),
    CONSTRAINT CK_DOIPHONG_KhacPhong CHECK (MaPhongCu <> MaPhongMoi)
);
GO

CREATE TABLE dbo.HOADON
(
    MaHD int IDENTITY(1,1) NOT NULL CONSTRAINT PK_HOADON PRIMARY KEY,
    MaThue int NULL,
    MaDatPhong int NULL,
    MaKH int NOT NULL,
    MaNV int NOT NULL,
    MaPhong int NULL,
    NgayLap datetime2(0) NOT NULL CONSTRAINT DF_HOADON_NgayLap DEFAULT (SYSDATETIME()),
    TongTienPhong decimal(18,2) NOT NULL CONSTRAINT DF_HOADON_TienPhong DEFAULT (0),
    TongTienDV decimal(18,2) NOT NULL CONSTRAINT DF_HOADON_TienDV DEFAULT (0),
    TongTienDichVu AS TongTienDV PERSISTED,
    TongPhuThu decimal(18,2) NOT NULL CONSTRAINT DF_HOADON_PhuThu DEFAULT (0),
    GiamGia decimal(18,2) NOT NULL CONSTRAINT DF_HOADON_GiamGia DEFAULT (0),
    TienCoc decimal(18,2) NOT NULL CONSTRAINT DF_HOADON_TienCoc DEFAULT (0),
    TienDatCocTruoc AS TienCoc PERSISTED,
    TienVat decimal(18,2) NOT NULL CONSTRAINT DF_HOADON_VAT DEFAULT (0),
    TongThanhToan decimal(18,2) NOT NULL CONSTRAINT DF_HOADON_Tong DEFAULT (0),
    TongTien AS TongThanhToan PERSISTED,
    DaThanhToan decimal(18,2) NOT NULL CONSTRAINT DF_HOADON_DaThanhToan DEFAULT (0),
    TrangThai nvarchar(20) NOT NULL CONSTRAINT DF_HOADON_TrangThai DEFAULT (N'Chưa thanh toán'),
    LoaiThanhToan nvarchar(20) NOT NULL CONSTRAINT DF_HOADON_Loai DEFAULT (N'CHECKIN'),
    PhuongThuc nvarchar(50) NULL,
    GhiChu nvarchar(1000) NULL,
    CONSTRAINT FK_HOADON_PHIEUTHUE FOREIGN KEY (MaThue) REFERENCES dbo.PHIEUTHUE(MaThue),
    CONSTRAINT FK_HOADON_DATPHONG FOREIGN KEY (MaDatPhong) REFERENCES dbo.DATPHONG(MaDatPhong),
    CONSTRAINT FK_HOADON_KHACHHANG FOREIGN KEY (MaKH) REFERENCES dbo.KHACHHANG(MaKH),
    CONSTRAINT FK_HOADON_NHANVIEN FOREIGN KEY (MaNV) REFERENCES dbo.NHANVIEN(MaNV),
    CONSTRAINT FK_HOADON_PHONG FOREIGN KEY (MaPhong) REFERENCES dbo.PHONG(MaPhong),
    CONSTRAINT CK_HOADON_TrangThai CHECK (TrangThai IN (N'Chưa thanh toán', N'Đã thanh toán', N'Đã hủy')),
    CONSTRAINT CK_HOADON_Loai CHECK (LoaiThanhToan IN (N'CHECKIN', N'PHATSINH', N'TONGHOP', N'HUY_GIU_COC')),
    CONSTRAINT CK_HOADON_Tien CHECK (TongTienPhong >= 0 AND TongTienDV >= 0 AND TongPhuThu >= 0 AND GiamGia >= 0 AND TienCoc >= 0 AND TienVat >= 0 AND TongThanhToan >= 0 AND DaThanhToan >= 0)
);
GO

CREATE UNIQUE INDEX UX_HOADON_MaThue_Active
ON dbo.HOADON(MaThue)
WHERE MaThue IS NOT NULL AND TrangThai <> N'Đã hủy';
GO

CREATE TABLE dbo.CHITIETHOADON
(
    MaCTHD int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CHITIETHOADON PRIMARY KEY,
    MaHD int NOT NULL,
    MaDVVT int NULL,
    NoiDung nvarchar(200) NOT NULL,
    SoLuong decimal(18,2) NOT NULL,
    DonGia decimal(18,2) NOT NULL,
    ThanhTien decimal(18,2) NOT NULL,
    CONSTRAINT FK_CTHD_HOADON FOREIGN KEY (MaHD) REFERENCES dbo.HOADON(MaHD) ON DELETE CASCADE,
    CONSTRAINT FK_CTHD_DVVT FOREIGN KEY (MaDVVT) REFERENCES dbo.DICHVUVATTU(MaDVVT),
    CONSTRAINT CK_CTHD_Tien CHECK (SoLuong > 0 AND DonGia >= 0 AND ThanhTien >= 0)
);
GO

CREATE TABLE dbo.CHITIETTHANHTOAN
(
    MaCTTT int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CHITIETTHANHTOAN PRIMARY KEY,
    MaHD int NULL,
    MaDatPhong int NULL,
    MaThue int NULL,
    NgayThanhToan datetime2(0) NOT NULL CONSTRAINT DF_CTTT_Ngay DEFAULT (SYSDATETIME()),
    SoTien decimal(18,2) NOT NULL,
    LoaiThanhToan nvarchar(50) NOT NULL,
    GhiChu nvarchar(500) NULL,
    CONSTRAINT FK_CTTT_HOADON FOREIGN KEY (MaHD) REFERENCES dbo.HOADON(MaHD),
    CONSTRAINT FK_CTTT_DATPHONG FOREIGN KEY (MaDatPhong) REFERENCES dbo.DATPHONG(MaDatPhong),
    CONSTRAINT FK_CTTT_PHIEUTHUE FOREIGN KEY (MaThue) REFERENCES dbo.PHIEUTHUE(MaThue),
    CONSTRAINT CK_CTTT_SoTien CHECK (SoTien >= 0)
);
GO

CREATE INDEX IX_DATPHONG_Lich ON dbo.DATPHONG(MaPhong, NgayNhanDuKien, NgayTraDuKien, TrangThai) WHERE MaPhong IS NOT NULL;
CREATE INDEX IX_CTDP_Lich ON dbo.CHITIETDATPHONG(MaPhong, NgayNhanDuKien, NgayTraDuKien);
CREATE INDEX IX_PHIEUTHUE_Lich ON dbo.PHIEUTHUE(MaPhong, NgayNhan, NgayTraDuKien, NgayTraPhong, TrangThai);
CREATE INDEX IX_HOADON_NgayLap ON dbo.HOADON(NgayLap, TrangThai);
GO

CREATE OR ALTER TRIGGER dbo.TR_DATPHONG_KhongTrungLich
ON dbo.DATPHONG
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.DATPHONG d
          ON d.MaDatPhong <> i.MaDatPhong
         AND d.MaPhong = i.MaPhong
         AND d.TrangThai IN (N'Đã đặt', N'Đã check-in')
         AND i.TrangThai IN (N'Đã đặt', N'Đã check-in')
         AND i.NgayNhanDuKien < d.NgayTraDuKien
         AND i.NgayTraDuKien > d.NgayNhanDuKien
        WHERE i.MaPhong IS NOT NULL
    )
    BEGIN
        THROW 51001, N'Phòng đã có đặt phòng trùng thời gian.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.PHIEUTHUE pt
          ON pt.MaPhong = i.MaPhong
         AND pt.TrangThai = N'Đang thuê'
         AND (pt.MaDatPhong IS NULL OR pt.MaDatPhong <> i.MaDatPhong)
         AND i.TrangThai IN (N'Đã đặt', N'Đã check-in')
         AND i.NgayNhanDuKien < ISNULL(pt.NgayTraPhong, pt.NgayTraDuKien)
         AND i.NgayTraDuKien > pt.NgayNhan
        WHERE i.MaPhong IS NOT NULL
    )
    BEGIN
        THROW 51002, N'Phòng đang có phiếu thuê trùng thời gian.', 1;
    END;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_CTDP_KhongTrungLich
ON dbo.CHITIETDATPHONG
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.DATPHONG dp ON dp.MaDatPhong = i.MaDatPhong
        CROSS APPLY
        (
            SELECT
                TuNgay = ISNULL(i.NgayNhanDuKien, dp.NgayNhanDuKien),
                DenNgay = ISNULL(i.NgayTraDuKien, dp.NgayTraDuKien)
        ) x
        JOIN dbo.CHITIETDATPHONG ct
          ON ct.MaCTDP <> i.MaCTDP
         AND ct.MaPhong = i.MaPhong
        JOIN dbo.DATPHONG dp2 ON dp2.MaDatPhong = ct.MaDatPhong
        CROSS APPLY
        (
            SELECT
                TuNgay = ISNULL(ct.NgayNhanDuKien, dp2.NgayNhanDuKien),
                DenNgay = ISNULL(ct.NgayTraDuKien, dp2.NgayTraDuKien)
        ) y
        WHERE dp.TrangThai IN (N'Đã đặt', N'Đã check-in')
          AND dp2.TrangThai IN (N'Đã đặt', N'Đã check-in')
          AND x.TuNgay < y.DenNgay
          AND x.DenNgay > y.TuNgay
    )
    BEGIN
        THROW 51003, N'Chi tiết đặt phòng bị trùng lịch phòng.', 1;
    END;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_PHIEUTHUE_KhongTrungLich
ON dbo.PHIEUTHUE
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.PHIEUTHUE pt
          ON pt.MaThue <> i.MaThue
         AND pt.MaPhong = i.MaPhong
         AND pt.TrangThai = N'Đang thuê'
         AND i.TrangThai = N'Đang thuê'
         AND i.NgayNhan < ISNULL(pt.NgayTraPhong, pt.NgayTraDuKien)
         AND ISNULL(i.NgayTraPhong, i.NgayTraDuKien) > pt.NgayNhan
    )
    BEGIN
        THROW 51004, N'Phòng đã có phiếu thuê trùng thời gian.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        JOIN dbo.DATPHONG dp
          ON dp.MaPhong = i.MaPhong
         AND dp.TrangThai = N'Đã đặt'
         AND i.TrangThai = N'Đang thuê'
         AND (i.MaDatPhong IS NULL OR dp.MaDatPhong <> i.MaDatPhong)
         AND i.NgayNhan < dp.NgayTraDuKien
         AND ISNULL(i.NgayTraPhong, i.NgayTraDuKien) > dp.NgayNhanDuKien
    )
    BEGIN
        THROW 51005, N'Phòng đã có đặt phòng trùng thời gian thuê.', 1;
    END;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_HOADON_DongBoThanhToan
ON dbo.HOADON
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE hd
       SET DaThanhToan =
            CASE
                WHEN hd.TrangThai = N'Đã thanh toán' THEN hd.TongThanhToan
                WHEN hd.TrangThai = N'Đã hủy' THEN 0
                ELSE hd.DaThanhToan
            END
    FROM dbo.HOADON hd
    JOIN inserted i ON i.MaHD = hd.MaHD
    WHERE (hd.TrangThai = N'Đã thanh toán' AND hd.DaThanhToan <> hd.TongThanhToan)
       OR (hd.TrangThai = N'Đã hủy' AND hd.DaThanhToan <> 0);
END;
GO

INSERT dbo.CALAM(TenCa, GioBatDau, GioKetThuc)
VALUES (N'Sáng', '07:00', '15:00'), (N'Chiều', '15:00', '23:00'), (N'Đêm', '23:00', '07:00');

INSERT dbo.NHANVIEN(HoTen, GioiTinh, NgaySinh, SDT, DiaChi, ChucVu)
VALUES
(N'Nguyễn Văn Quản', N'Nam', '1990-01-15', '0901000001', N'Hà Nội', N'Quản lý'),
(N'Trần Thị Lễ Tân', N'Nữ', '1998-06-20', '0901000002', N'Hải Dương', N'Lễ tân');

INSERT dbo.TAIKHOAN(TenDangNhap, MatKhau, VaiTro, MaNV)
VALUES (N'admin', N'123', N'Quản lý', 1), (N'letan', N'123', N'Nhân viên', 2);

INSERT dbo.LOAIPHONG(TenLoaiPhong, SoNguoiToiDa, DonGiaGio, DonGiaNgay, DonGiaDem, TienCocGoiY, MoTa)
VALUES
(N'Phòng đơn', 1, 80000, 450000, 350000, 200000, N'1 giường đơn'),
(N'Phòng đôi', 2, 120000, 700000, 500000, 250000, N'1 giường đôi'),
(N'Phòng VIP', 4, 200000, 1200000, 900000, 400000, N'Phòng cao cấp');

INSERT dbo.PHONG(TenPhong, Tang, MaLoaiPhong, TrangThai)
VALUES
(N'101', 1, 1, N'Trống'), (N'102', 1, 1, N'Trống'), (N'103', 1, 1, N'Trống'),
(N'201', 2, 2, N'Trống'), (N'202', 2, 2, N'Trống'), (N'203', 2, 2, N'Trống'),
(N'301', 3, 3, N'Trống'), (N'302', 3, 3, N'Trống');

INSERT dbo.DICHVUVATTU(TenDVVT, Loai, DonGia, DonViTinh, SoLuongTon, TonToiThieu)
VALUES
(N'Nước suối 500ml', N'Vật tư', 10000, N'Chai', 100, 20),
(N'Nước ngọt', N'Vật tư', 15000, N'Lon', 100, 20),
(N'Mì ly', N'Vật tư', 20000, N'Ly', 50, 10),
(N'Giặt ủi', N'Dịch vụ', 50000, N'Lần', 0, 0);
GO
