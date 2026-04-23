# LiveWallpaper (v3.2)

Desktop live wallpaper engine berbasis **mpv** untuk Windows 10/11. 
Putar video sebagai wallpaper desktop dengan dukungan multi-monitor, playlist, scheduler, dan kontrol penuh.

---

## Download Jadi (Siap Pakai)
Males build sendiri? Kamu bisa langsung ambil file installernya di sini:
**[Download via Google Drive](https://drive.google.com/drive/folders/1bw4czk7HeGtxF7x2ryqUJfMqGQZh53J3?usp=sharing)**

## Fitur 


| Fitur | Keterangan |
|-------|------------|
| **Ultra Light RAM** | Penggunaan resource sangat efisien (**~41 MB** pada pengujian terbaru). Sangat ringan! |
| **Smart Pause** | Video otomatis *pause* saat ada aplikasi di-maximize untuk menghemat GPU/Baterai. |
| **Custom Icon** | Bisa ganti ikon aplikasi sesuka hati (cek bagian Persiapan). |
| **Multi-Monitor** | Mendukung pengaturan wallpaper berbeda untuk tiap monitor. |
| **Full Control** | Kontrol Volume, Speed, Brightness, Contrast, dan Posisi Video (Pan X/Y). |

---

## Persiapan Sebelum Build

### 1. Install .NET 9 SDK
Download di: [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
Pilih **Windows x64**. Verifikasi dengan ketik `dotnet --version` di CMD (harus muncul angka `9.x`).

### 2. Download libmpv
Download dari [shinchiro build](https://sourceforge.net):
- Ekstrak file `.7z` tersebut.
- Ambil file **`libmpv-2.dll`**.
- **Wajib:** Salin ke folder root project (sejajar dengan `LiveWallpaper.csproj`).

### 3. Custom App Icon (PENTING)
Kamu bisa mengganti ikon aplikasi dan tray menu dengan milikmu sendiri:
- Siapkan file gambar format **`.ico`** (Wajib `.ico`, bukan png/jpg).
- Rename menjadi **`app_icon.ico`**.
- Timpa/Ganti file ikon lama di folder root project.
- Ikon ini akan otomatis tertanam ke dalam `.exe` saat proses build.

### 4. Install Inno Setup 7
Diperlukan jika kamu ingin membuat file Installer (`.exe` setup).
Download di: [jrsoftware.org](https://jrsoftware.org/isinfo.php). Install dengan pengaturan default.

---

## Struktur Folder Project

Pastikan susunan folder kamu seperti ini agar tidak error saat build:

```text
LiveWallpaper/
  Engine/            (Logic Win32 & Mpv)
  Pages/             (UI/XAML Pages)
  App.xaml
  LiveWallpaper.csproj
  libmpv-2.dll       <-- Hasil download (Wajib ada)
  app_icon.ico       <-- Ikon custom kamu
  setup.iss          <-- Script Inno Setup
  build.bat          <-- Script build otomatis
  build_installer.bat
```

---

## Cara Build & Distribusi

### A. Build Biasa (Untuk Test)
Jalankan `build.bat` atau ketik:
```bash
dotnet build -c Release
```
Hasil ada di: `bin\Release\net9.0-windows\win-x64\`

### B. Build Installer (Untuk Dibagikan)
Double-click **`build_installer.bat`**. Script ini akan:
1. Melakukan `dotnet publish` (Self-contained, user tidak perlu install .NET lagi).
2. Menjalankan Inno Setup untuk membungkus semuanya jadi satu file.
3. Hasilnya ada di folder **`installer\LiveWallpaper_Setup_v3.1.exe`**.

---

## Penggunaan Sumber Daya

Aplikasi ini sangat ramah RAM:
- **RAM:** Stabil di kisaran **41 MB - 160 MB** (tergantung resolusi video).
- **GPU:** Menggunakan hardware acceleration, sehingga CPU tetap rendah.

---

## Lisensi & Aturan (Free to Use)

Project ini menggunakan lisensi MIT untuk kode C# dan LGPL untuk mpv. 
**Bebas dipakai, dimodif, atau dirusak suka hati awokwok.** 

**Satu Syarat:** 
Jangan hapus **Watermark (WM)** atau kredit asli di bawah ini. Hargai tukang ketiknya!

**Original Author:**
> **MockingCLOWN - LOUDERS YOAKIM TELAUMBANUA**

---

*Made with KEGABUTAN by MockingCLOWN*
