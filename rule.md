# Development Rules

## Database Operations
- **Rule**: Selalu gunakan Neon MCP untuk semua operasi database
- **Tools**: `mcp__neon__*`
- **Alasan**: Konsistensi dan memanfaatkan fitur MCP Neon yang sudah terintegrasi

## Documentation & References
- **Rule**: Selalu gunakan MCP Context7 untuk referensi library, framework, SDK, API, CLI tool, atau cloud service
- **Tools**: `mcp__context7__*`
- **Alasan**: Mendapatkan dokumentasi terkini dan akurat

## UI/UX Patterns
- **Rule**: Selalu gunakan Select2 untuk dropdown/select elements
- **Alasan**: User experience yang lebih baik dengan search, pagination, dan styling yang konsisten
- **Implementation**:
  - Tambahkan Select2 CSS & JS (CDN atau local)
  - Inisialisasi dengan class `.select2` atau selector spesifik
  - Gunakan tema `bootstrap-5` untuk konsistensi dengan Bootstrap 5
  - Untuk modal, gunakan `dropdownParent` option agar render dengan benar
- **Contoh**:
  ```javascript
  // Basic select
  $('.my-select').select2({
      theme: 'bootstrap-5',
      width: '100%'
  });

  // Select2 dalam modal
  $('.modal-select').select2({
      theme: 'bootstrap-5',
      dropdownParent: $('#myModal'),
      width: '100%'
  });
  ```

## Code Quality
- **Rule**: Selalu jalankan build setelah menulis kode
- **Alasan**: Memastikan tidak ada error sebelum commit
- **Implementation**: Gunakan `Bash` tool untuk menjalankan perintah build setiap kali selesai menulis/mengubah kode

## Workflow
1. Gunakan Context7 untuk referensi sebelum menulis kode
2. Gunakan Neon MCP untuk operasi database
3. Jalankan build setelah selesai menulis kode
4. Pastikan build success sebelum melanjutkan

## Notes
- File ini dapat di-enhance kapan saja sesuai kebutuhan
- Tambahkan aturan baru di bagian atas dengan kategori yang sesuai
