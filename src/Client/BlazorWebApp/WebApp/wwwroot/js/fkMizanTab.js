window.fkMizanTab = {
    scrollToRow: function (rowId) {
        const el = document.getElementById(rowId);
        if (!el) return;
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
};
