# Cel Isko

**Prosta, lekka i zoptymalizowana przeglądarka na TV: wchodzę na stronę,
wybieram transmisję lub film i oglądam płynnie. Ochrona przed reklamami
i popupami jest włączona domyślnie.**

Główną grupą odbiorców są użytkownicy w Polsce. Polski jest podstawowym językiem
interfejsu i komunikatów; angielski ułatwia współpracę i korzystanie spoza Polski.
Projekt opisujemy przez ten sposób korzystania, bez listy docelowych serwisów.

## Najważniejszy scenariusz

1. Otwieram stronę adresem, kafelkiem lub ulubionym.
2. Pilotem wybieram transmisję na żywo albo film.
3. Uruchamiam odtwarzanie i przechodzę na pełny ekran.
4. Oglądam bez narastającego opóźnienia, zacięć powodowanych przez przeglądarkę,
   reklam i wyskakujących okien zasłaniających lub przerywających materiał.
5. Wracam do sterowania, zmieniam materiał albo zamykam stronę. Poprzedni dźwięk
   i odtwarzacz są zatrzymane, a aplikacja pozostaje responsywna.

To kryteria docelowe. Obecna alpha nie ma jeszcze dowodu ich spełnienia na TV.

## Priorytety implementacji

- **Płynność:** szybka reakcja na pilot podczas ładowania strony i odtwarzania,
  ograniczona kolejka wejścia, mało pracy na głównym wątku i stabilne użycie pamięci.
- **Odtwarzanie:** zachowanie odtwarzacza strony, zgodność ramek i fullscreen,
  poprawne sprzątanie po zmianie materiału, zamknięciu i ukryciu aplikacji.
- **Ochrona od pierwszego uruchomienia:** blokowanie reklam, popupów i niechcianych
  przekierowań jako podstawowa część produktu, z prostym wyjątkiem dla witryny.
- **Prostota:** krótka droga do oglądania i minimum ustawień potrzebnych użytkownikowi.
  Każdą dodatkową funkcję oceniamy pod kątem wpływu na ten scenariusz i zasoby TV.

## Co oznacza ochrona w rodzaju uBlocka

Chodzi o skuteczność i wygodę domyślnej ochrony, nie sam napis „adblock”. Potrzebne
są odpowiednio szerokie reguły żądań, obsługa popupów i przekierowań oraz usuwanie
pozostałości reklam, przy zachowaniu działania właściwego odtwarzacza. Dobór list,
ich licencje, aktualizowanie i koszt przetwarzania wymagają osobnego projektu.
Nie deklarujemy zgodności z rozszerzeniem ani wykorzystania jego silnika.

W obecnym kodzie ochrona jest domyślnie włączona, ale ma mały zestaw reguł.
Nie stanowi jeszcze kompletnego rozwiązania. Skuteczność trzeba wykazać kontrolami
pozytywnymi i negatywnymi: reklama/popup zablokowane, właściwe żądanie i materiał
działają, wyjątek witryny przywraca oczekiwane zachowanie.

## Jak oceniamy postęp

Mierzymy czas do interakcji i pierwszego obrazu, opóźnienie reakcji na pilot,
stabilność obrazu/dźwięku i zmiany pamięci podczas długiego oglądania oraz kolejnych
otwarć i zamknięć stron. Porównania wykonujemy w tych samych warunkach, z ochroną
włączoną i wyłączoną oraz z przeglądarką systemową. Najpierw ustalamy bazę pomiarową,
potem progi akceptacji; nie publikujemy wymyślonych wyników ani obietnic wydajności.

Problemy sieci i witryny rozróżniamy od narzutu Isko. Testy przenośne pomagają
sprawdzać logikę; płynność, skuteczność ochrony i odtwarzanie wymagają osobnych
obserwacji na TV. Dostęp do urządzenia wymaga wyraźnej zgody użytkownika.

[Plan prac](roadmap.md) · [Stan weryfikacji](testing.md) · [Obecna ochrona](protection.md)
