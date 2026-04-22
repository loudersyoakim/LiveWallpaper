# LiveWallpaper (v3.1)

Desktop live wallpaper engine berbasis **mpv** untuk Windows 10/11. 
Putar video sebagai wallpaper desktop dengan dukungan multi-monitor, playlist, scheduler, dan kontrol penuh.

---

## Fitur Unggulan


| Fitur | Keterangan |
|-------|------------|
| **Hemat RAM** | Penggunaan resource sangat rendah (**~40 MB - 100 MB**). Ringan untuk multitasking! |
| **Single & Playlist** | Putar satu video loop atau seluruh isi folder secara berurutan. |
| **Auto-switch** | Ganti video otomatis dengan timer (10 detik s/d 1 hari). |
| **Smart Pause** | Video otomatis *pause* saat ada aplikasi lain di-maximize (hemat GPU). |
| **Custom Icon** | Bisa ganti ikon aplikasi sesuka hati (cek bagian Persiapan). |
| **Full Control** | Kontrol Volume, Speed, Brightness, Contrast, dan Posisi Video (Pan X/Y). |

---

## Persiapan Sebelum Build

### 1. Install .NET 9 SDK
Download di: [dotnet.microsoft.com](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) (Pilih x64).

### 2. Download libmpv
Download file `libmpv` terbaru (misal versi 2026 atau terbaru) dari [shinchiro build](https://sourceforge.net). 
**Wajib:** Salin file `libmpv-2.dll` ke folder root project (sejajar dengan `.csproj`).

### 3. Ganti Ikon Suka-Suka (Custom Icon)
Kamu bisa mengganti ikon aplikasi dan tray menu dengan ikon buatanmu sendiri:
- Siapkan file gambar dalam format **`.ico`** (Wajib `.ico`, bukan `.png` atau `.jpg`).
- Beri nama file tersebut: **`app_icon.ico`**.
- Timpa/Ganti file lama di folder project dengan file milikmu.
- Saat di-build (Publish), ikon tersebut akan otomatis tertanam di file `.exe` dan muncul di system tray.

---

## Cara Build & Run

### Build Cepat (Portable)
Buka terminal di folder project, ketik:
```bash
dotnet build -c Release
```
Hasil build ada di `bin\Release\net9.0-windows\win-x64\`.

### Build Installer (.exe Setup)
Double-click file **`build_installer.bat`**. 
Script ini akan menghasilkan satu file installer di folder `installer/` yang sudah *self-contained* (tidak perlu install .NET lagi di PC lain).

---

## Penggunaan Sumber Daya (Resource Usage)

Aplikasi ini sudah dioptimasi habis-habisan:
- **RAM:** Stabil di kisaran **41 MB** (bisa naik sedikit tergantung resolusi video). Ini jauh lebih kecil dibanding browser atau aplikasi wallpaper lainnya.
- **GPU:** Menggunakan hardware acceleration via `libmpv`, beban CPU tetap hampir 0%.

---

## Lisensi & Aturan (Free to Use)

Project ini bersifat **Open Source**. Kamu bebas menggunakan, menyebarkan, bahkan memodifikasi kodenya sesuka hati (Open for modification). 

**Satu syarat aja awokwok:** 
Jangan hapus **Watermark (WM)** atau kredit asli dari project ini!

**Original Author:**
> **MockingCLOWN - LOUDERS YOAKIM TELAUMBANUA**

---

*Made with GABUT and a lot of Coffee by MockingCLOWN*
