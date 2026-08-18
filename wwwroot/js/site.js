// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// ===== EduTrack UX yaxshilashlari =====

document.addEventListener('DOMContentLoaded', function () {
    // 1) Forma yuborilganda tugmani vaqtincha o'chiramiz.
    // Bu bir xil ma'lumotni ikki marta yuborishning oldini oladi.
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

    // 2) Muvaffaqiyat/xato xabarlari avtomatik yo'qolsin.
    document.querySelectorAll('.alert-success, .alert-danger').forEach(function (alertEl) {
        setTimeout(function () {
            alertEl.style.transition = 'opacity 0.5s ease';
            alertEl.style.opacity = '0';
            setTimeout(function () {
                alertEl.remove();
            }, 500);
        }, 4000);
    });

    // 3) data-confirm atributiga ega amallar uchun tasdiqlash.
    document.querySelectorAll('[data-confirm]').forEach(function (el) {
        el.addEventListener('click', function (event) {
            if (!window.confirm(el.getAttribute('data-confirm'))) {
                event.preventDefault();
            }
        });
    });

    // 4) Mobil sidebar/hamburger menyusi.
    // Muhim: bu kod data-confirm siklidan tashqarida turishi kerak.
    // Aks holda sahifada data-confirm elementi bo'lmasa, menyu listeneri umuman ulanmaydi.
    var sidebarToggle = document.getElementById('izSidebarToggle');
    var sidebar = document.getElementById('izSidebar');
    var backdrop = document.getElementById('izBackdrop');

    if (!sidebarToggle || !sidebar || !backdrop) {
        return;
    }

    function setSidebarState(isOpen) {
        sidebar.classList.toggle('iz-open', isOpen);
        backdrop.classList.toggle('iz-show', isOpen);
        sidebarToggle.setAttribute('aria-expanded', String(isOpen));
        sidebarToggle.setAttribute(
            'aria-label',
            isOpen ? 'Menyuni yopish' : 'Menyuni ochish'
        );
        document.body.classList.toggle('iz-sidebar-open', isOpen);
    }

    function toggleSidebar(event) {
        if (event) {
            event.preventDefault();
            event.stopPropagation();
        }
        setSidebarState(!sidebar.classList.contains('iz-open'));
    }

    sidebarToggle.addEventListener('click', toggleSidebar);
    backdrop.addEventListener('click', function () {
        setSidebarState(false);
    });

    // Menyu bandi tanlanganda mobil sidebar yopiladi.
    sidebar.querySelectorAll('.iz-nav-item').forEach(function (link) {
        link.addEventListener('click', function () {
            setSidebarState(false);
        });
    });

    // Escape tugmasi bilan ham sidebar yopiladi.
    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape' && sidebar.classList.contains('iz-open')) {
            setSidebarState(false);
        }
    });

    // Katta ekranga qaytilganda mobil holatni tozalaymiz.
    window.addEventListener('resize', function () {
        if (window.innerWidth > 991) {
            setSidebarState(false);
        }
    });
});
