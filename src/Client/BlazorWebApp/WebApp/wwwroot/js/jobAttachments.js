// Randevu modalı için görsel/belge yakalama: Ctrl+V yapıştır + sürükle-bırak.
// Yakalanan dosyayı data-URL'e (base64) çevirip .NET tarafına geçirir.
// Tıkla-yükle Blazor InputFile ile yapılır; burada JS gerekmez.
window.jobAttachments = {
    _ref: null,
    _zone: null,
    _onPaste: null,
    _onDrop: null,
    _onDragOver: null,
    _onDragLeave: null,
    _accept: ['image/png', 'image/jpeg', 'image/jpg', 'application/pdf'],

    register: function (dotNetRef, dropZone) {
        this.unregister();
        this._ref = dotNetRef;
        this._zone = dropZone;

        // Ctrl+V (ana yöntem): panodaki görseli yakala
        this._onPaste = function (e) {
            var items = e.clipboardData && e.clipboardData.items;
            if (!items) return;
            for (var i = 0; i < items.length; i++) {
                if (items[i].kind === 'file') {
                    var file = items[i].getAsFile();
                    if (file) window.jobAttachments._send(file);
                }
            }
        };
        document.addEventListener('paste', this._onPaste);

        // Sürükle-bırak
        if (dropZone) {
            this._onDragOver = function (e) { e.preventDefault(); dropZone.classList.add('ja-dragover'); };
            this._onDragLeave = function () { dropZone.classList.remove('ja-dragover'); };
            this._onDrop = function (e) {
                e.preventDefault();
                dropZone.classList.remove('ja-dragover');
                var files = e.dataTransfer && e.dataTransfer.files;
                if (files) for (var i = 0; i < files.length; i++) window.jobAttachments._send(files[i]);
            };
            dropZone.addEventListener('dragover', this._onDragOver);
            dropZone.addEventListener('dragleave', this._onDragLeave);
            dropZone.addEventListener('drop', this._onDrop);
        }
    },

    _send: function (file) {
        if (!file || this._accept.indexOf(file.type) === -1) return;
        var reader = new FileReader();
        reader.onload = function () {
            if (!window.jobAttachments._ref) return;
            window.jobAttachments._ref.invokeMethodAsync(
                'OnAttachmentCaptured',
                file.name || 'ekran-goruntusu.png',
                file.type,
                reader.result,   // "data:...;base64,...."
                file.size);
        };
        reader.readAsDataURL(file); // paste → base64
    },

    unregister: function () {
        if (this._onPaste) document.removeEventListener('paste', this._onPaste);
        if (this._zone) {
            if (this._onDragOver) this._zone.removeEventListener('dragover', this._onDragOver);
            if (this._onDragLeave) this._zone.removeEventListener('dragleave', this._onDragLeave);
            if (this._onDrop) this._zone.removeEventListener('drop', this._onDrop);
        }
        this._ref = null; this._zone = null;
        this._onPaste = null; this._onDrop = null; this._onDragOver = null; this._onDragLeave = null;
    }
};
