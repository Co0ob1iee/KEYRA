# **KEYRA — Pełna Dokumentacja Projektowa i Architektura Systemu**

## **1\. Tożsamość i Wizja Marki**

**KEYRA** to nowoczesny, minimalistyczny menedżer kluczy SSH zaprojektowany z myślą o deweloperach, administratorach systemów oraz zespołach DevOps, które stawiają na najwyższy poziom bezpieczeństwa i wygodę pracy (DevEx).

* **Misja:** Uprościć zarządzanie tożsamościami kryptograficznymi i wyeliminować chaos w katalogach \~/.ssh, przy jednoczesnym zagwarantowaniu wojskowego poziomu szyfrowania lokalnej bazy danych.  
* **Obietnica marki:** Szybkość, pełna kontrola nad tożsamościami, zero zbędnych rozpraszaczy.  
* **Tone of Voice:** Profesjonalny, precyzyjny, nowoczesny, zorientowany na technologię i zaufanie.

## **2\. Brand Pack & System Wizualny**

### **2.1 Koncepcja Logo i Ikony**

* **Sygnet (Ikona):** Geometryczne połączenie litery **K** z elementem cyfrowego klucza oraz zarysem tarczy ochronnej. Linia pionowa litery K przechodzi w symetryczny grot symbolizujący szyfrowany strumień danych lub znak terminala (\>).  
* **Logotyp:** Czysty krój bezzyeryfowy (Sans-Serif) z powiększonym odstępem między znakami (letter-spacing). Pogrubiona sylaba **KEY** odróżnia się od końcówki **RA** jaśniejszym akcentem kolorystycznym.  
* **Ikona Aplikacji (App Icon):** Zaokrąglony kwadrat (squircle) na ciemnym tle (\#0B0F19 do \#161B22) z centralnie umieszczonym sygnetem w kolorze **Cyber Emerald** (\#10B981).

### **2.2 Paleta Kolorów**

| Rola w UI | Nazwa koloru | Kod HEX | Zastosowanie |
| :---- | :---- | :---- | :---- |
| **Tło główne** | Deep Slate | \#0B0F19 | Główne tło okna aplikacji, terminala |
| **Powierzchnia** | Card Dark | \#161B22 | Panele, listy serwerów, karty kluczy |
| **Kolor wiodący (Primary)** | Cyber Emerald | \#10B981 | Aktywny stan SSH, przyciski akcji, sukces |
| **Kolor akcentu (Secondary)** | Electric Blue | \#3B82F6 | Statusy szyfrowania, linki, ikony informacyjne |
| **Stan krytyczny** | Crimson Red | \#EF4444 | Błędy autoryzacji, usuwanie kluczy |
| **Tekst główny** | Off-White | \#F3F4F6 | Główna treść, nazwy kluczy i hostów |
| **Tekst poboczny** | Muted Gray | \#9CA3AF | Ścieżki plików, opisy, metadane |

### **2.3 Typografia**

* **Interfejs Użytkownika (UI):** **Inter** lub **SF Pro** – czysty, nowoczesny krój bezzyeryfowy o wysokiej czytelności.  
* **Dane Techniczne, Klucze i Terminal:** **JetBrains Mono** lub **Fira Code** – krój monospace ze wsparciem dla ligatur oraz wyraźnym odróżnieniem cyfry ![][image1] od litery ![][image2].

## **3\. Architektura Szyfrowania (Security & Crypto)**

Zgodnie z zasadą **Zero-Knowledge Architecture**, master password podany przez użytkownika **nigdy nie jest zapisywany na dysku**. Służy wyłącznie jako dane wejściowe do wygenerowania symetrycznego klucza szyfrującego w pamięci RAM na czas trwania aktywnej sesji.

### **3.1 Derwacja Klucza z Master Password (KDF)**

Przekształcenie hasła mastera w ![][image3]\-bitowy Master Encryption Key (**MEK**) odbywa się za pomocą algorytmu **Argon2id**:

* **Memory Cost (![][image4]):** ![][image5] (![][image6])  
* **Time Cost (![][image7]):** ![][image8] iteracje  
* **Parallelism (![][image9]):** ![][image10] wątki  
* **Salt:** ![][image11] bajtów (CSPRNG)

### **3.2 Szyfrowanie Kopertowe (Envelope Encryption)**

1. **Master Encryption Key (MEK):** Generowany z Argon2id.  
2. **Database Key (DBK):** Unikalny ![][image3]\-bitowy klucz symetryczny generowany przy tworzeniu bazy.  
3. **Encrypted DBK:** Klucz DBK zaszyfrowany za pomocą MEK przy użyciu **AES-256-GCM**.  
4. **Data Encryption:** Poszczególne pola wrażliwe (klucze prywatne, passphrase'y) szyfrowane są kluczem DBK za pomocą algorytmu **AES-256-GCM** (![][image12]\-bajtowy Nonce, ![][image11]\-bajtowy Auth Tag).

\+------------------+     Argon2id \+ Salt  
| Master Password  | \---------------------\> \[ MEK \]  
\+------------------+                          |  
                                              v (AES-256-GCM)  
                                    \+--------------------+  
                                    | Encrypted DBK      | \---\> \[ DBK \]  
                                    \+--------------------+        |  
                                                                  v (AES-256-GCM)  
                                                       \+--------------------+  
                                                       | Private Key Data   |  
                                                       \+--------------------+

### **3.3 Bezpieczeństwo Pamięci RAM**

* **Memzero:** Wszystkie bufory RAM zawierające deszyfrowany MEK, DBK oraz klucze prywatne są nadpisywane zerami (0x00) natychmiast po użyciu lub zablokowaniu aplikacji.  
* **Locked Memory (mlock):** Bloki pamięci z kluczami są blokowane w RAM za pomocą mlock() / VirtualLock(), zapobiegając ich zrzuceniu do pliku SWAP/pagefile.

## **4\. Schemat Bazy Danych (SQLite Schema)**

\-- Ustawienia skarbca i zaszyfrowany klucz bazy (Envelope Encryption)  
CREATE TABLE vault\_metadata (  
    id INTEGER PRIMARY KEY CHECK (id \= 1),  
    salt BLOB NOT NULL,                    \-- 16B Salt dla Argon2id  
    argon\_memory INTEGER NOT NULL,         \-- Pamięć w KiB (np. 65536\)  
    argon\_iterations INTEGER NOT NULL,     \-- Liczba powtórzeń (np. 3\)  
    argon\_parallelism INTEGER NOT NULL,    \-- Liczba wątków (np. 4\)  
    enc\_dbk BLOB NOT NULL,                 \-- DBK zaszyfrowany za pomocą MEK  
    dbk\_nonce BLOB NOT NULL,               \-- Nonce 12B dla enc\_dbk  
    dbk\_tag BLOB NOT NULL,                 \-- Auth Tag 16B dla enc\_dbk  
    created\_at DATETIME DEFAULT CURRENT\_TIMESTAMP  
);

\-- Zarządzanie Kluczami SSH  
CREATE TABLE ssh\_keys (  
    id TEXT PRIMARY KEY,                   \-- UUID v4  
    name TEXT NOT NULL,                    \-- Nazwa własna (np. "Prod K8s Admin")  
    key\_type TEXT NOT NULL,                \-- ed25519, rsa\_4096, ecdsa\_p384, sk-ed25519  
    public\_key TEXT NOT NULL,              \-- Jawny klucz publiczny (OpenSSH format)  
    fingerprint\_sha256 TEXT NOT NULL,      \-- Odcisk palca SHA256  
      
    \-- Pola zaszyfrowane (AES-256-GCM z użyciem DBK)  
    enc\_private\_key BLOB NOT NULL,         \-- Zaszyfrowany klucz prywatny lub Key Handle  
    private\_key\_nonce BLOB NOT NULL,       \-- IV (12 bajtów)  
    private\_key\_tag BLOB NOT NULL,         \-- Auth Tag (16 bajtów)  
      
    enc\_passphrase BLOB,                   \-- Zaszyfrowany passphrase (opcjonalnie)  
    passphrase\_nonce BLOB,  
    passphrase\_tag BLOB,

    comment TEXT,  
    created\_at DATETIME DEFAULT CURRENT\_TIMESTAMP,  
    updated\_at DATETIME DEFAULT CURRENT\_TIMESTAMP  
);

\-- Lista Serwerów i Połączeń SSH  
CREATE TABLE servers (  
    id TEXT PRIMARY KEY,                   \-- UUID v4  
    title TEXT NOT NULL,                   \-- Etykieta (np. "Production App Server 01")  
    host TEXT NOT NULL,                    \-- IP lub FQDN  
    port INTEGER NOT NULL DEFAULT 22,      \-- Port SSH  
    username TEXT NOT NULL,                \-- Użytkownik  
      
    default\_key\_id TEXT,                   \-- Powiązany klucz SSH z tabeli ssh\_keys  
    proxy\_jump\_id TEXT,                    \-- Identyfikator serwera JumpHost (Bastion)  
    tags TEXT,                             \-- Tagi w formacie JSON np. \["prod", "aws"\]  
    notes TEXT,  
      
    created\_at DATETIME DEFAULT CURRENT\_TIMESTAMP,  
    FOREIGN KEY (default\_key\_id) REFERENCES ssh\_keys(id) ON DELETE SET NULL,  
    FOREIGN KEY (proxy\_jump\_id) REFERENCES servers(id) ON DELETE SET NULL  
);

\-- Historia i Logi Połączeń (Audit Log)  
CREATE TABLE connection\_logs (  
    id INTEGER PRIMARY KEY AUTOINCREMENT,  
    server\_id TEXT NOT NULL,  
    connected\_at DATETIME DEFAULT CURRENT\_TIMESTAMP,  
    status TEXT NOT NULL,                  \-- 'SUCCESS', 'FAILED', 'TIMEOUT'  
    error\_message TEXT,  
    FOREIGN KEY (server\_id) REFERENCES servers(id) ON DELETE CASCADE  
);

## **5\. Architektura Połączeń SSH przez JumpHost (Bastion Server)**

KEYRA realizuje połączenia przez serwery pośredniczące za pomocą mechanizmu **Direct TCP Forwarding** (kanał direct-tcpip), eliminując konieczność kopiowania kluczy na Bastion.

\+-------------------------------------------------------------------------------+  
| KEYRA Client                                                                  |  
|                                                                               |  
|  1\. Auth: Key A  \---------------\> \[ Bastion Server:22 \]                       |  
|                                         |                                     |  
|                                         | 2\. Tunnel Request (direct-tcpip)    |  
|                                         v                                     |  
|  3\. Auth: Key B \------------=====\> \[ Target Server:22 \] (End-to-End Encrypted)|  
\+-------------------------------------------------------------------------------+

### **Przepływ Połączenia:**

1. **Bastion Handshake:** KEYRA nawiązuje sesję SSH z Bastionem przy użyciu klucza *Key A*.  
2. **Kanalizowanie (direct-tcpip):** KEYRA wysyła pakiet SSH\_MSG\_CHANNEL\_OPEN z żądaniem otwarcia surowego gniazda TCP do Target\_IP:22.  
3. **End-to-End SSH:** Wewnątrz utworzonego tunelu KEYRA inicjuje drugą, w pełni odizolowaną sesję SSH z serwerem docelowym, uwierzytelniając się kluczem *Key B*. Bastion nie ma dostępu do *Key B* ani treści przesyłanych komend.

## **6\. Integracja z Agentami SSH oraz Kluczami Sprzętowymi YubiKey**

### **6.1 Systemowy SSH Agent (ssh-agent / Pageant)**

* **Tryb Klienta:** KEYRA odczytuje tożsamości z socketu systemowego (UNIX socket na Linux/macOS lub Named Pipe na Windows) i zleca podpisanie wyzwań bez odczytywania kluczy prywatnych.  
* **Tryb Dostawcy (Provider):** KEYRA może utworzyć własny socket agenta (\~/.keyra/agent.sock), udostępniający tożsamości z bazy zewnętrznym narzędziom CLI (ssh, git), wyłącznie gdy skarbiec pozostaje odblokowany.

### **6.2 Klucze Sprzętowe (FIDO2 & PKCS\#11)**

* **FIDO2 (sk-ed25519 / sk-ecdsa-sha2-nistp256):** Klucz prywatny jest generowany i przechowywany wewnątrz tokena YubiKey. Baza KEYRA zapisuje wyłącznie klucz publiczny oraz tzw. *Key Handle*.  
* **PKCS\#11 / PIV SmartCard:** Obsługa certyfikatów zagnieżdżonych w slotach PIV (np. Slot 9a).  
* **User Presence (Touch):** Uwierzytelnienie wymaga fizycznego dotknięcia płytki YubiKey. Operacja kryptograficzna odbywa się wewnątrz mikrokontrolera tokena.

## **7\. Specyfikacja Interfejsu Zarządzania YubiKey**

### **7.1 Układ Panelu Ustawień (UI Layout)**

\+---------------------------------------------------------------------------------------------------+  
|  HARDWARE SECURITY KEYS                                           \[ \+ Pair New Hardware Key \]     |  
\+---------------------------------------------------------------------------------------------------+  
|  \[●\] YubiKey 5C NFC (Serial: 14829104\)  •  Status: Ready          \[ Test Touch \]  \[ Settings \]    |  
\+---------------------------------------------------------------------------------------------------+  
|                                                                                                   |  
|  REGISTERED HARDWARE KEYS (2)                                                                     |  
|                                                                                                   |  
|  \+---------------------------------------------------------------------------------------------+  |  
|  | \[🔑\] YubiKey 5C \- Main (FIDO2 / ED25519-SK)                              \[ Active Device \]   |  |  
|  |      Public Key:   sk-ssh-ed25519@openssh.com AAAAC3NzaC1lZDI1NTE5AAAA...                     |  |  
|  |      Fingerprint:  SHA256:7uX9kQ2LzN8p3R1...                                                   |  |  
|  |      Policies:     \[ Touch Required: YES \]  \[ PIN Cached: NO \]                                 |  |  
|  |                                                                        \[ Test \]  \[ Delete \]    |  |  
|  \+---------------------------------------------------------------------------------------------+  |  
|                                                                                                   |  
|  \+---------------------------------------------------------------------------------------------+  |  
|  | \[💳\] YubiKey PIV Certificate (PKCS\#11 Slot 9a)                        \[ Device Disconnected \]|  |  
|  |      Subject:      CN=DevOps Admin (PIV Card Holder)                                           |  |  
|  |      Fingerprint:  SHA256:1a2b3c4d5e...                                                       |  |  
|  |      Policies:     \[ Touch Required: NO \]   \[ Slot: 9a \]                                       |  |  
|  |                                                                        \[ Test \]  \[ Delete \]    |  |  
|  \+---------------------------------------------------------------------------------------------+  |  
|                                                                                                   |  
\+---------------------------------------------------------------------------------------------------+

### **7.2 Przepływ Kreatora Parowania**

1. **Wybór standardu:** FIDO2 (sk-ed25519) vs PKCS\#11 (PIV).  
2. **Autoryzacja PIN:** Zabezpieczone pole tekstowe do wprowadzenia PINu sprzętowego z licznikiem pozostałych prób.  
3. **Wyzwanie dotknięcia:** Pulsacyjna animacja z zieloną diodą z napisem: *"Dotknij płytki YubiKey, aby zatwierdzić operację..."* z odliczaniem 30 sekund.  
4. **Import:** Odczyt klucza publicznego i zapis rekordu w bazie KEYRA.

[image1]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAoAAAAaCAYAAACO5M0mAAABf0lEQVR4Xo1SsUrEQBDdFAcKB3JgsElmshEUO3GxtbrGQhG0Uv9DsD1s/AHB7j7CykJL4Wq1t7GyM42NvszuTjYRwUkm7Lx9O+9lEmM0Mn9nsfKXgEpon1q3a2UnoYyuRygH+wFsa4FEbSgb18iyLJaJ6ZSZb5HX1trNqKLcuq5XsHlPxLM8z8dVVW2jfkEeR47IgHABcMFEE3/cGGI+Q75W1q4JwMQTIlqgyzy1VBTlLjN9lmV5KACKLeSHEDseVMhBpYHKlSARsELM9O1ZcGpUCcQDEL9bQAYQ5GMDJaLYp4QY59URrbfkAUjYzmNqSfF/EzGeGsR3zGsuDK+POZLDfBvYuhQoz1fHID4CvLPWLnmH0mAK/As5DbZF5hz5BtCGlhlkZ8in9vMGEWOccyMQb7DxAE9HQiJ+br+5epHjfnjZel1vYNAn8LvndtyoY3jar1UcesQSRvJn60YWb0UGkR4Kqz/Z3vIQ9RH/mgQZrGOd4H3HvQjtepaSAPADFXpN2Wk8R54AAAAASUVORK5CYII=>

[image2]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAaCAYAAAC+aNwHAAACMUlEQVR4Xn1TPWsbQRC9Q3YRiAtjiaDT7exuCAiRyjqcyrgQwl06d/4D7lOlc+MiTQpjCKhJmZ9gSBVIp3QBkcKB4CYGG9s/wCDnze3efumcgb3bfTPvzcx+ZJm1vB78DSy365w9iY/h1mWANnwPt1NM9tDn5qYut/IFBdZKNIvBYLAlRLlPRAdSyhHcnUgy0La/utdca72ntJ6DdE5SHpKkQ6XUV6wvRCl2HC1taVyN10H8gMA/JjB3MVVVrQOfYdxjbDMWVVKBjFI/SaI7VPBmRR1rKWkE8i3GmaV5p1bqCAFLlHpUe3xXboEEm2jpB5IsBkXRteQsK0X5Cqp/4fyltXphzrwZ1jDFXmxKFpDyEqPvfMh6jOyPAE+ivprsFkQFLxF3JQkCZAV6vd5zZP4GYIkM04YbmT14kKdIssT4PhwON3LGQeoTl0T15owSqjXTFPynGI9csYVrsA9l7umSuK/6LnhiY0VRCCS5QMIbIvk6dHRBXJiNob55NqtNIOt7CPA+vUudaxD4AvIDNmk32kQ70bgX0lygGV+oYF9NAErahsA9BD7zhQqrAGmCcY0kH8uyfOZ5iQkhdhD4G6XO+e6bNyDPlVY/IT7JXFKbPn6SBsenwyfBrw8ibyFaNJ42M5QUZVtR93HmH37DafiLGPzI7b8G0gR5CsXa7lgj0SfNeKMq2qxp838xSSEx0Kz4E1UWAh6NK7KLVcyZd/oDCSIY9xMT1SpkA51InKW2fxn6W6e/8KoRAAAAAElFTkSuQmCC>

[image3]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAB8AAAAZCAYAAADJ9/UkAAADoElEQVR4XrVUPWgUQRTeJUkRjIgkl+Pudmd278SQSs2KoigIYhoJiBIESeEPqNjpFfG3sLCwUdQIEgQRi4Ba2FiIgukUA2JhsBKJBCwkKQIRGxO/tzNvZ3d2L17jg3c7773v/c6bcxzXiUl/kq85FIr/h4qSFOmKySDb92mD0sGKAiudNUYmy9kl2cbkFLZsU6Fdl2HXkcOmFFLKCviaFGIyDILrnudtMlaNEWJUSLm7VCr1QHQbjUa/EGIsCIKtCUjH9HyvG/GOgSfBN2q1mqeM1HqCdpwwDHcA8Nr3/b1hEG7B+SV4Fdw04ZxOJHmq9WChv/JZvV7fYDUyCP4E/CVKigJHcf5Qb9T7yc5TcdBhNzp6AfBJiB2kq1VrvQRGl8twjFJBH4I/Q/cdtufgg+zDNdI0CEOJSVkul9dBfiOtWDFeqnHPIdASdc0m6C5TZ9Bf4Eoh38sGyBPs48AtwG8wpduPq7xIjSqNDhgNRV2o8g4Ar6SQFTbGQQSSCzmudCY5T83eIRo/MO/BMxh3LzFNwuHp6CtvQckydEo11j8obB/roZtAMbdx3x/xnYf8DryNvaGnu15QBYj7KP4KFQyeNUvpmjtXYpaCINwJh2XwZBRFXWxH149CdZcdejpjKHAxxMIqu4zA8BMr8D2s3Vwkvgn5i9r4NUiPjhbkCS1LurLNAwPrIXawioIBRxOYgthJV6KLnq1Wa33sJ6QYgW4VRZxhnSIdiT5RNNSFAA8AvOUny5HGZGeELisY7RzwX4MwLOtnugSeLvWVelITG6GnieSPY4U9alo8SqyfSNwdggzi7Q8TGPoTkFfwPcs+Ur8UXUAlDIMyFRInV39EMaU6V8nThNgujE0YzzupdaAxSX13tP1CBUiS89ihn+5TyWhRpzCRGeA3Mk51rsZuTc+lhTgO4y90MC/oD0Qz5EVUvYfKw3kXMBO0gOwJv6PQ/QYfYR0SH4D8E378JFssnEsjkRVBo+O/TcF/nzH/QBF1RkNugt+CTyH4XaGe1TmaHAek4qC7Cv6GQk5L9dTmcX3bc3edIRMlnn5yASmnarXqB2FwyPf8YXoZBs8/arA1wgXAYWfiV5OjdCUFiUzALHGKrMXIxR7s15JamBOVZS8+ZsjUb4HtSl2z6Ba5xqEVZE3STkX+iZyrKI1U5wz2H2RCZX1buhYZciUUgVJketCnlhO1qC1YWyBH5S3Q5SipNCmUUfbXkOkur82fLQ0OfwEZxso25MOzcgAAAABJRU5ErkJggg==>

[image4]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABIAAAAaCAYAAAC6nQw6AAAB/UlEQVR4XpVUzytFQRR2Q1F+JN7Cu+/OzL1+vJSinpSysaMkyVZZkbIiWZD8DXZkY/92JMlC+Qss7LEkKQslG98Zd97MnTtz46vpnPnOOd+ZM3O7TU0eBDYhkbLSuDMKEHjqLEJvzcDffHet3cGGL/wfDY1sYuZEbg3/OG7KK2TCvAsfXPOpDgZ8Qjl+cGiwi3M+F5bLEe1rtVprJYomhBCL5ZSjgyXJwDBjbDlJkmHJNAC3VCp1oOAYCQecsWfYFYiew65xxvfgv8Kfhz3B2o+FWIV9xNrWQgACsyIW63EcjzHOP5Bwg47dFINQP+fsCfwL4pOqBo3PkHfbh0M0zgVyg0QQWELBN+yMKoA/AqE3nGhXFVAxiWDVsW3JXRQKjhB8wJ30qZeKomgB3BdGntZ5EGf8DXYz91bVarUTgTumuqQAR+KPlUolVJ1JAOsdTUZVngTF0UGOgISt34LAGIHVg1Qcgu0Y84qW9DnfiYW+OxJa4nQ/jRECYwS2pUagZwf/So9DnwWETunVG18CLvwQCfdhGPYaI8xhfWJNpYeUnwr2l8i/hr2AHVcT/ArFoo2S9OVJrxkj9NBG8xLN9CD00cqdFZSwhBw7ky9WyBI5PotM2BzRhI/XKAxquMcxYZ26cOrCoIVcZ0dxYY6bsOCr88IscFSZvPazv+AfzM5U2tCHcpIAAAAASUVORK5CYII=>

[image5]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAADsAAAAZCAYAAACPQVaOAAAE/ElEQVR4Xs1WTahVVRQ+h56QGEnk8+J79559zn1vkKAYXEkcSyKIIZkRBQ0apIMgUPKnQgQLNFLIlxDqxEG8hg1DHAhChE4a9DACieJRVKgUOtCo/L79c87+Pe/eq4O+x3rn7LW+tfZee62978myNuS+IkTukYZwcTGywyOHWoH9f2QYt3Hcc8+9jjFOMB+PIsZSGGKOISg1Qq7psdAS142Nhw82VIQIyVf54yS63e5yIcSrkLOQD6enp7sOwYtUluVO8D5yTLlLg/0U5D7kPy3zYEzEuNTA/il5heQW9DuFefbi/Y4Vg8LxIuQf/X5OrjeZLQ16QpDXQr5F4MN0KopiN96v9mdmVgc++AdbCf4NPC449hpqVjM34n2GBH6Dz69479scA9gqyDeQv5Hc5cnJySfsXYT/u0wU/jtsPxRpFrof4HO7qqrnTF5RzCAhBPmOiWKYdzqdFRhfgtxFkIHPHwwGy2A7z4nLsmqS1Rsh/3uTgTuHWEfw/BfylmtVgP0l2N7nvBAvWWk/iISCZAnwX+N6hOycTHWOhspdB1JBxE3IWkOAbguSP9TtdZcbXW0TxW5wT0IWVWW9zHIruIYoxBw2ZhsWu1AnYgFxHheq5beDo5Jd5XL0OpGs2OFPSZ1ONohNyM3v9/srhWyd4hra9+kpCCsN82O+A8H2RTudxeMZ+P3ktrG7AnskVGUH4B/F+z3IZtve6/XWYcFvk4ONaSproUk2qCzP+jGd7H7PppHXZ5VV5Vk5A3mPC8OEC1jYszZdt+/H0G/Cc42wkvU22oWaZw4dMZDJqDY9bVOgP4A5N1t2K1kV3Ur2dT3/Gt4vPH54/xPyDteYXAyDFzyb6iy9qNU5ApzA5NftGxn2XZB9tJtkK1PZ1AQSeV1Z3vh4foXx9aqqOrTq7jqmbZFkVXiTLNZ2UahfDCNXYPuSRcj882PDCr4wNTW1ylDZKjrwHk6FhWEoznFhtAuvslFY0+KYyGTVu7pM4PsKx3jiLIs3ZEIiniwRa2M9RS47AwXDvXAikxdUJOeqrDaA9JcJnofJXoDfBMbH5c7pIEy2SCRbT+Mkqyqr37lz/I2cZ9shQf6kVLTh3C6ZLDbOP7MZCwUbLz95H8STRSvBeMMPbierf5rYKj8bwfgXNbG4zzF2dK8dV6GZUFjJ0iDUx8NtXEwv4PmBIctOMxeUcxvn0coacO3wuwyJ2s1SJhBgHnINpKeMzUp2j9HZEFYb23sY2U+JJlkyctO6aLvyKt8NTybLyhbpyhaRyqLaplvuFbKyCSDI8yD9Ye98WeGCEu4FVQPrpV4H/1xp0uh2e7x4vuA8RicvpUL+AshNNgHqZL2PCj5SlZ2dnX2S65AfHOiYLPioUH8S+ieFXy4/ItCbgj89SAQtttF1yrLO6s4KLly437u3UJ1oG2PTPpELbLiLuHXX04oKHYTfUb7ro/K1UN+6dVyhv43h/7vR61i34M/jRA7H32NdLyNU9Psg+KTDIe8h8E4kuZWfjK41hlhBfZ0//l8hvThj8TdJoVHyzaWkRtFADhQjzmuztWNJH6vvx8IwvjFOTNcgtY2t8Gn+2EWbNW6La9tR+7Q4t5gcxHm21vRrnGmhISQXKMdj9EbSIWmIYBRuApEUR8QIfoY6gsvwiASNqNoxskMD3QjuOAVt9H2iCCvUeIe+oSaKYWgBJ51dQPU1ISFQPQBJXmVPV0qhlAAAAABJRU5ErkJggg==>

[image6]: <data:image/png;base64,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>

[image7]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAcAAAAcCAYAAACtQ6WLAAABPElEQVR4Xo1RO04DQQydLSJFggKJhBW7rD0DN8hcISdAQqJD4gTUdBRpKFOmp+AAqeAadEg0SJwBaHj2fDdIiF15x37PfrZ3TGPSE73RMcaCMwr0wNvIV4KiVxJ3/KLSFEIKo0ZKMYaJr5n5qeu7wxo3zrkpEW+ZaWutnf6DjGzf9yeQfYfsSicXfDaf7xPzMRFdgPi2zl5CpV14PzGQuAK4gb0h6RP2AIX1MAxnOrf2Qy8kxH5xL7GuQz8O/aoZg8dES8h9QW6Z8cRihVsQHxjqVBH9tzBMh37Yj+i5PWr3PKZEfC+tDH7VDIu/oHolw6HvOQpuQikQZ+0dEl5xPorv/WJSxhJ5aw/E0g5K6REv99cdp9uvTb+h4x9kjHZIcaqEnJiSEznKruMKK7GaVsZbr8ncIyM5O+KN+QGGyC9fVMf3awAAAABJRU5ErkJggg==>

[image8]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAoAAAAaCAYAAACO5M0mAAABsklEQVR4Xo1SPUsDQRC9QwKCXwQTAhduZyd3GgUhmuvFTtJYWdhbaCfBwtrSwiallUEE/4B9wC7+BLVJYSeikEaQ+Hb3bm8vWrjsZWfevHkzsxvP83y9zfJz07ju4S4FKXIxpH0DTee4VRxsykh1/3DUGa/Ei0KIEyK6klKeN6Jo1YQcdWZukRADnDsgl0E8IhJfJOgUrLxlgD0ofYdhuKd8QaIM/xHEN0G0rkngewAuEZiAcKiwZrO5gOQH+J/MsmUk8SVJUgqCoAJvxkcmlDeg9A7yoFqtzJu67njYcYyhiG7xjaC2WaAos1arzWGQO5QboZUXDLTrqQo5yRD1TlEpeQ3kV3w3SsByLEkTfX0hpMoLmkD52A7zL2LSTkpwusyy24at+WCi3zNMDSL3dWVkJehljCnHeJ3EtOujT9mHP0Gsp4n1IAgBPEHhOmpESxqr15dBHJK+S9rKhlRP2AHwjOAFSz6AfQ/sA2cnbdyUUUuynGWW6k+xz1Juq9dKdfJlpDP9FCv8FmP5yhKn466gvVw7gcWctIKZZVoFG9EXbcWyQFH893J4Lmjc4nSOB+MH4ydJAtbDQFQAAAAASUVORK5CYII=>

[image9]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAoAAAAbCAYAAABFuB6DAAABqElEQVR4XpVTsUoDQRDdw1gEgwjmOMnt7uxJGutDK8sg2FhYCYJYaW8jaCNIwB8QIfgHprWz0M4P8AMsxFSSDxCJ72Z3c7fnNU7Y2503b2be3F5EJGD8mG98qOIec88qO6oizrzHVVw4YPxJsYQ5HNW7NJqt3VgscDyBF5K4ekixLSt4KaNBh8ddQe9Hot/vLxPRbtrrqQJKkmRJKblDpHO4C8xTUrZBusW6IK0/tdY3mmgM0iEVu6ZRnueLAsAAgUsAG9i/ELyXSOYiSm0CmyJ5IIzJjrMs2wK4B9IPg04cztso8A38jMUXhspDreldKpX6yUxmTkGcgbj/D2Jkp4SWJ4BjAC1HbBU+FbqhnzOhZR3OBFOfW1KEDpRhfWjSdzYZJiUPMjPGXLlqEfxryHnDoDS/AYBDgFPsr1gPOL+g0hiJa16viLtxB22fQXiUSrbTtLfajeOOjTpSYayPaKJZX2iuoeBbxwAnmBivQB+h1YonuC+yTIGWEYij4k7x3g7811LSqilBwPpu0tDqpMZq5Udqf0GWOztKDeWj+58E7QN6PVj2+gWcPEiox3JgMAAAAABJRU5ErkJggg==>

[image10]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAoAAAAaCAYAAACO5M0mAAABTUlEQVR4Xo1TMVIDMQw8F6nTpeKkO19NATeBD/ADhj/QQsPAH/hAUvAYZnhIyhSpODomrC1blm+uQIlGlrRaSR5f0zSuEUnWZZvPLp0TTDEZKBH5Z2BMmbyLP3Vswop4dSwF6hFc471fE/NHe9FeakLa1n27rntl4omIRhlkQbq+u2XmE3RiDsC5oMwPfo3kHqBdAAbGpaUcks/Qe2J6ycDZArHlDeZ6H8dxBVuAFhdagmXXY4vgR0Zdpgi27J9wHQ9h+VBNkZEmxEa9ELS4kpbXq1wpM1JaJl3Pv4EY6xGBA8BQOlC0/A09o/UR/ucwDJtq83yOM5IwLqTDJnJG2zeM9NO27daEnb43MNwBcISek/5CvwbvN/Wtq2QKky4PR7ar6+zLCTMEYxHqLLabSSIoTCViRCHWtZ4UlXiKWfrCYRL6nWajcUMXj2XrP77hQND189OwAAAAAElFTkSuQmCC>

[image11]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABUAAAAYCAYAAAAVibZIAAACCElEQVR4XoVTsUrEQBBNUEFBsLkrjOduElG5wsYrRAsrGwtB0OLgSgtFO23trLS0EUSEQ/wCe7G3EUEbK+HAH9DC4jjf7O1md5JNfDDJ7sybt282d0FACLOHBzrPXzmE1RJFeJjuAZ61g+rqEG6+YgK74UJWu+yAqgrBaY4VknauyHZJkixKKS6klNdSyB1kRzgDQLGJOEQ8gtSHaNetG7RarTFwThHPQogWIgX/AS4OOBNAoYnCtpByDeteXtRoUzMEP2gW1SflJmKAbZfdlLuB6DREPzOSg2gmmiVBxKXhp2k6Rc7BX1GJfBNBkqgcitrskIlcG/c4QL0TJ/E4cev1+qTllUCJKqd6fA2SJYc0KuIMnDu8jxAvOOyc7joj5u1WOk3irhZ9Mg4bjcY89l+IE8s3LfphRblTAh1E4+O9P8yEAYnTIYi3KIpq3KTeKFH9ofJl1I6VUyG2TL6WiYof+omxBvNQX9/jVIuuIn7B6RgT1qnwONUwoiDdB6TlkHB/E6g9IG6wHdW57E4tVa+EFBsQ6qHYx/gDGhMHfOMaXtG4ZOjg0D/vHXGL2KM6eq+yr69g5F3beu2bhLAMgSRO1nGHu3NpuhCUU4uoZHpMsEThLv5x6tfzZytQTlKVQpkleNXvgsOKuuOFHuNu3YXfVgZmooxbSBVOL+tlCfZbd+DmOaNM9A9OYG0SI0QEeAAAAABJRU5ErkJggg==>

[image12]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABUAAAAYCAYAAAAVibZIAAAB8ElEQVR4XpVTsUrEQBDN4QlaCBaC3iXZSVA4LKxOLMTyCsFG0MLCVgt/wNLKLxAsRJArbETwB8RC/AARWwuPA8FCG63k0LfJJjvZ3VzOd2z2dubt27eTieflqOH3Hyi+fuTxAqpJxQ1ufikcQkP3cdcuWAnHAY5ViWh6WpQg3jGzCmNEtIFxSiROhRDbQRhMOm8D0iJIB4LojgQNINrlJDm12+1xcE4gdBSG4bwQtAf+N/jPcRyTVlOQorC3CdIqSP1clAH5dfBugyDwsxgO2EXsF+McyzqjayDZgOirS1SQOGQCnvTv+36AdR/jBW5nHTWVGzPRqGvmceVlbH4kIfazWGYCsxwNp6h2aoumKEZxwBr2/GDcYM9EzjFeWC6a5csgXxxqegH+J66+YuYVaqkopTXVelxZf8rgbWG8odadYksZTpKaJqJFp5wbwRU4D+iCBRWqyy7Anul0acBdU/0J4pqg0LWcVchrNpsziJ21Wq2p3CQ3m10fTX6ZpJhFHDSHcY/8B5z10Pw9vKge1u+IX3lmn4LUobTfBpT2ohxfURw94ZpLipP1qRqC/z+2y+moHYcjZIGXqzDbCYahoQpXGay8FShDFccS5f+tgBGvELe2l/ELeuqhuY6THUJWaESTnumz2gSbRwU3b0Tz6Q89F2h/AqecbQAAAABJRU5ErkJggg==>