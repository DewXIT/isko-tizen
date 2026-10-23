# Isko Tizen — przeglądarka na Samsung Smart TV

Otwarty projekt przeglądarki na **Tizen 9**, napisany w **C# / .NET**, z silnikiem
**NUI WebView**. Główny cel: **wchodzę na stronę, wybieram transmisję lub film
i oglądam płynnie**, z ochroną przed reklamami i popupami włączoną domyślnie.
Liczą się prostota, małe zużycie zasobów i szybka reakcja na pilot. Główną grupą
odbiorców są użytkownicy w Polsce. [Pełny opis kierunku projektu](vision.md).

Isko ma natywny kursor, pasek adresu i wyszukiwania, ekran główny, ulubione,
historię i pasek sterowania pokazywany na żądanie. Zawiera też wczesną ochronę,
domyślnie włączoną: małą wbudowaną listę hostów reklamowych, politykę popupów opartą na
ostatnim naciśnięciu pilota, usuwanie nakładek na całe okno i pułapek na kliknięcia oraz
odmowę pobierania plików. Wyjątek dla strony wyłącza filtry. To jeszcze nie pełna ochrona
w rodzaju uBlocka; patrz [protection](protection.md). Interfejs jest obecnie głównie po polsku.

## Stan projektu

**Wczesna alpha, dostępna jako kod źródłowy.** Kompilacje i testy lokalne przechodzą,
ale zgodność ze stronami oraz obsługa pilota i wideo wymagają weryfikacji na TV.
Dźwięk przy czarnym obrazie lub loaderze pozostaje nierozwiązanym problemem zgodności.
Nie ma gwarancji działania każdego serwisu streamingowego.

## Instalacja na własnym TV

Potrzebujesz TV z Tizen 9, Node.js, .NET SDK 8 oraz narzędzi Samsunga do budowania
i podpisywania aplikacji. Tworzysz własny profil certyfikatu dla swojego telewizora.

```sh
git clone https://github.com/DewXIT/isko-tizen.git
cd isko-tizen
mkdir -p .local
cp tools/tv/config.example.json .local/isko-tv.json
# Uzupełnij wyłącznie prywatną kopię konfiguracji.
node tools/tv/doctor.cjs          # sprawdza narzędzia, profil i połączenie z TV, mówi co naprawić
node tools/tv/package.cjs --dry-run
node tools/tv/package.cjs
# Jawne połączenie z TV, instalacja i uruchomienie:
node tools/tv/package.cjs --install
```

[Pełna instrukcja krok po kroku](install.pl.md) opisuje narzędzia, certyfikat Samsung, telewizor i konfigurację.
Budowanie bez `--install` nie łączy się z TV. Konfiguracja, certyfikaty i gotowe
pakiety pozostają lokalne; nie wysyłaj ich do repo ani do zgłoszeń.

## Współpraca

[Plan prac](roadmap.md) · [Zasady współpracy](../CONTRIBUTING.md) ·
[Zgłoszenia](https://github.com/DewXIT/isko-tizen/issues/new/choose) ·
[Bezpieczeństwo](../SECURITY.md) · [English README](../README.md)

Zgłoszenia po polsku i angielsku są mile widziane. Usuń z opisów i zdjęć prywatne
adresy sieci, identyfikatory urządzeń, dane kont oraz tokeny. Pomóc można także bez
TV: przy dokumentacji, tłumaczeniach, testach przenośnych i kontrolowanych stronach.

Licencja [MIT](../LICENSE). Niezależny projekt społecznościowy; nie jest produktem Samsunga.
