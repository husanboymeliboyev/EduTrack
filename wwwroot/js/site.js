// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// ===== UX yaxshilashlari =====

document.addEventListener('DOMContentLoaded', function () {

    // 1) Forma yuborilganda "Saqlash/Yaratish" kabi tugmalarni vaqtincha o'chirib,
    //    yuklanish holatini ko'rsatamiz. Bu foydalanuvchi tugmani bir necha marta
    //    bosib, bir xil ma'lumotni ikki marta yubormasligi uchun kerak.
    document.querySelectorAll('form').forEach(function (form) {
        form.addEventListener('submit', function () {
            var submitBtn = form.querySelector('button[type="submit"]');
            if (submitBtn && !submitBtn.disabled) {
                submitBtn.dataset.originalText = submitBtn.innerHTML;
                submitBtn.disabled = true;
                submitBtn.innerHTML =
                    '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Yuklanmoqda...';
            }
        });
    });

    // 2) Muvaffaqiyat/xato xabarlari (alert) bir necha soniyadan keyin avtomatik yo'qolsin,
    //    shunda sahifa toza ko'rinishda qoladi.
    document.querySelectorAll('.alert-success, .alert-danger').forEach(function (alertEl) {
        setTimeout(function () {
            alertEl.style.transition = 'opacity 0.5s ease';
            alertEl.style.opacity = '0';
            setTimeout(function () {
                alertEl.remove();
            }, 500);
        }, 4000);
    });

    // 3) "O'chirish" kabi og'ir oqibatli amallar uchun qo'shimcha tasdiqlash so'rovi
    //    (agar tugmada data-confirm atributi bo'lsa).
    document.querySelectorAll('[data-confirm]').forEach(function (el) {
        el.addEventListener('click', function (e) {
            if (!confirm(el.getAttribute('data-confirm'))) {
                e.preventDefault();
            }
            // 4) Mobil ekranda "hamburger" tugmasi sidebar'ni ochib/yopib turadi.
            //    Fon (backdrop) bosilganda ham sidebar yopiladi.
            var sidebarToggle = document.getElementById('izSidebarToggle');
            var sidebar = document.getElementById('izSidebar');
            var backdrop = document.getElementById('izBackdrop');

            function openSidebar() {
                sidebar.classList.add('iz-open');
                backdrop.classList.add('iz-show');
            }

            function closeSidebar() {
                sidebar.classList.remove('iz-open');
                backdrop.classList.remove('iz-show');
            }

            if (sidebarToggle && sidebar && backdrop) {
                sidebarToggle.addEventListener('click', function () {
                    if (sidebar.classList.contains('iz-open')) {
                        closeSidebar();
                    } else {
                        openSidebar();
                    }
                });

                backdrop.addEventListener('click', closeSidebar);

                sidebar.querySelectorAll('.iz-nav-item').forEach(function (link) {
                    link.addEventListener('click', closeSidebar);
                });
            }
        });
    });
});
