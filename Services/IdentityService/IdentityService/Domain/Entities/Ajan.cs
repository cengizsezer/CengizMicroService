using System;

namespace IdentityService.Domain.Entities
{
    /// <summary>
    /// Ofisteki makinede günlerce ayakta duran ajanın (PkfRobot) kimliği.
    ///
    /// Kullanıcı token'ı 20 dakika yaşıyor; ajan onunla bağlansa yirmi dakikada
    /// bir düşerdi. Ayrıca o makine fiziksel olarak erişilebilir bir yerde
    /// duruyor: orada kullanıcı parolası ya da uzun ömürlü kullanıcı token'ı
    /// tutmak, bir insanın bütün yetkisini masaüstüne bırakmak demek. Bu yüzden
    /// ajanın kendine ait, iptal edilebilir bir anahtarı var.
    ///
    /// Anahtarın kendisi <b>saklanmıyor</b> — parolada olduğu gibi yalnız hash'i
    /// tutuluyor (<see cref="AnahtarHash"/>). Kaybolursa yenisi üretilir.
    /// </summary>
    public class Ajan
    {
        public int Id { get; set; }

        /// <summary>Kullanıcının verdiği ad: "Ofis Banka PC".</summary>
        public string Ad { get; set; } = default!;

        /// <summary>Ham anahtarın hash'i. Ham anahtar hiçbir yerde durmuyor.</summary>
        public string AnahtarHash { get; set; } = default!;

        /// <summary>
        /// Anahtarın ilk 8 karakteri. Yalnız iki iş için var: listede hangi
        /// anahtarın hangi kayıt olduğunu göstermek ve token isteğinde hash
        /// doğrulamasına girecek adayları daraltmak.
        /// </summary>
        public string AnahtarOnEki { get; set; } = default!;

        public int OlusturanKullaniciId { get; set; }

        /// <summary>UTC. Depoda saat dilimi tutulmuyor; alanların tamamı UTC.</summary>
        public DateTime OlusturmaZamani { get; set; }

        /// <summary>Anahtarla en son ne zaman token alındı; hiç alınmadıysa null.</summary>
        public DateTime? SonKullanim { get; set; }

        /// <summary>Anahtarın son geçerlilik anı; null ise süresiz.</summary>
        public DateTime? GecerlilikBitisi { get; set; }

        public bool Aktif { get; set; } = true;

        public DateTime? IptalZamani { get; set; }
        public string? IptalNedeni { get; set; }
    }
}
