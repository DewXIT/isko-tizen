# Budowanie i instalacja na własnym TV

Projekt jest dystrybuowany jako kod źródłowy. Każdy użytkownik buduje i podpisuje aplikację **własnym** profilem
certyfikatu Samsung dla **swojego** telewizora. Podpisany pakiet (TPK) jest prywatny i nie działa na cudzym TV.
Repozytorium nie zawiera adresów urządzeń, certyfikatów, haseł ani danych konta.

English version: [install.md](install.md).

## Krótka ścieżka

1. **Narzędzia** (raz): Node 16+, .NET SDK 8, workload Samsung Tizen i narzędzia wiersza poleceń Tizen SDK (`sdb`, `tz`).
2. **Profil certyfikatu Samsung dla TV** (raz; potrzebne konto Samsung i DUID telewizora).
3. **Telewizor**: tryb deweloperski włączony, z adresem IP tego komputera.
4. **Prywatna konfiguracja** `.local/isko-tv.json` (ignorowana przez Git).
5. **Sprawdzenie**: `node tools/tv/doctor.cjs` mówi, czego brakuje i jak to naprawić.
6. **Budowa, podpis, instalacja**: `node tools/tv/package.cjs --install`.

Narzędzia i profil certyfikatu trzymaj w **stałych** folderach. Folder tymczasowy (`/tmp`) system czyści, a utraconego
certyfikatu autora nie da się odtworzyć z tego repozytorium.

## 1. Narzędzia

- **Node.js 16+** i **.NET SDK 8** ([pobierz](https://dotnet.microsoft.com/download/dotnet/8.0)).
- **Workload Samsung Tizen .NET** (obsługa `net6.0-tizen9.0`): instalator opisuje
  [wiki Samsunga](https://github.com/Samsung/Tizen.NET/wiki/Installing-Tizen-.NET-Workload). To skrypt uruchamiany w
  powłoce, więc pobierz go i przeczytaj przed uruchomieniem. Sprawdź: `dotnet workload list` (ma pokazać `tizen`).
- **Narzędzia wiersza poleceń Tizen SDK**: `sdb` i `tz`. Nie są osobnymi pobraniami, są w instalatorze CLI SDK:

  ```sh
  node tools/tv/setup-tools.cjs   # pobiera oficjalny instalator (z kontrolą rozmiaru), niczego nie instaluje
  ```

  Skrypt wypisze kolejne polecenia. Licencję przeczytasz przez `--show-license`, a instalacja z
  `--accept-license <folder>` oznacza, że **Ty** ją akceptujesz. Zainstaluj w stałym folderze, np. `~/tizen-sdk`.
  `sdb` jest w `tools/`, `tz` w `tools/tizen-core/`. Źródło: `https://download.tizen.org/sdk/Installer/`.
- Do dodania **rozszerzenia TV** lub graficznego tworzenia certyfikatów potrzebny jest menedżer pakietów z tego samego SDK
  (`package-manager/package-manager-cli.bin`) albo pełne IDE [Tizen Studio](https://developer.tizen.org/).

## 2. Profil certyfikatu

Profil Samsung ma **certyfikat autora** (identyfikuje Ciebie, jest związany z kontem Samsung) i **certyfikat
dystrybutora** (autoryzuje Twój telewizor po DUID). Tworzą je narzędzia Samsunga. Polecenie `tz cert` robi zwykły
certyfikat Tizen, **nie** Samsunga, więc do TV nie wystarczy.

1. Na TV: **Menu > Wsparcie > Kontakt z Samsung > Unikalny identyfikator urządzenia** (DUID). Zapisz go.
2. W **Tizen Studio** (Tools > Certificate Manager) albo w **rozszerzeniu Samsung TV dla VS Code**: dodaj profil,
   wybierz **Samsung**, **TV**, nazwij profil, utwórz **nowy certyfikat autora** (zalogujesz się kontem Samsung i
   wskażesz **ścieżkę kopii zapasowej**), potem **certyfikat dystrybutora** z DUID. Instrukcje Samsunga:
   [Tizen Studio](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/creating-certificates.html),
   [VS Code](https://developer.samsung.com/smarttv/develop/tools/additional-tools/vscode/creating-certificates.html).
3. **Zrób kopię `author.p12` i jego hasła** w bezpiecznym, trwałym miejscu (aktualizacje muszą być podpisane tym samym
   certyfikatem autora). Nigdy nie wkładaj certyfikatów, haseł ani folderu profilu do repozytorium.
4. Profil ma być widoczny dla `tz`: `tz security-profiles list` powinno go pokazać. Jeśli powstał w Tizen Studio, `tz`
   czyta jego plik profili. W innym razie dodaj go przez `tz security-profiles add`
   ([polecenia Tizen-Core CLI](https://docs.tizen.org/application/tizen-studio/tizen-core/tizen-core-cli/)).

## 3. Telewizor

- TV z **Tizen 9**. Wejdź w **Aplikacje**, wpisz na pilocie **12345**, włącz **Developer mode** i wpisz **IP komputera**,
  na którym budujesz. Zrestartuj TV, gdy o to poprosi. (Po odcięciu zasilania sprawdź, czy IP nadal tam jest.)
- Komputer i TV muszą być w tej samej sieci. `sdb connect <ip-tv>:26101`, a potem `sdb devices` ma pokazać telewizor.

## 4. Prywatna konfiguracja

```sh
mkdir -p .local
cp tools/tv/config.example.json .local/isko-tv.json
```

Edytuj kopię. Przykład (własne ścieżki bezwzględne i nazwy; `192.0.2.50` to adres dokumentacyjny, wpisz adres swojego TV):

```json
{
  "dotnet": "dotnet",
  "tz": "/sciezka/do/tizen-sdk/tools/tizen-core/tz",
  "sdb": "/sciezka/do/tizen-sdk/tools/sdb",
  "signingProfile": "MojProfilTv",
  "deviceAddress": "192.0.2.50"
}
```

Plik nie przyjmuje haseł ani kluczy. Sprawdź całość (niczego nie zmienia, nie wypisuje adresu i nie czyta kluczy):

```sh
node tools/tv/doctor.cjs
```

## 5. Budowa i instalacja

```sh
node tools/tv/package.cjs --dry-run   # sprawdza plan, niczego nie wykonuje
node tools/tv/package.cjs             # buduje i podpisuje, nie łączy się z TV
node tools/tv/package.cjs --install   # łączy się z TV, instaluje i uruchamia (wyłącznie na Twoje wyraźne polecenie)
```

Wynik trafia do `artifacts/` (ignorowane przez Git). Budowa wyłącza inspektor i symbole debugowania, mapuje ścieżki
źródeł i audytuje pakiet przed instalacją. Docelowy framework to `net6.0-tizen9.0`: wersja `net8.0-tizen9.0` się
instaluje, ale na przetestowanym TV nie startuje.

## Najczęstsze problemy

| Objaw | Przyczyna i naprawa |
| --- | --- |
| `doctor`: narzędzie „w folderze tymczasowym" albo `tz`/`sdb` „not found" | Narzędzia z `/tmp` zostały wyczyszczone. Zainstaluj ponownie w stałym folderze i popraw `.local/isko-tv.json`. |
| `Signing profile exists: not listed` | Magazyn profili zniknął lub został przeniesiony. Utwórz profil od nowa (krok 2) albo zaimportuj swoją kopię `author.p12`. Certyfikatów Samsung nie da się odtworzyć z tego repozytorium. |
| `sdb connect` nie działa po restarcie TV | Stare połączenie jest nieaktualne: `sdb disconnect`, `sdb kill-server`, `sdb start-server`, `sdb connect <ip>:26101`. Jeśli port jest otwarty, a nadal nie działa, sprawdź w Aplikacje > 12345, czy **Developer mode** jest ON z IP komputera. Po zmianie zrestartuj TV. |
| Błąd instalacji o certyfikacie lub uprawnieniach | Certyfikat dystrybutora musi zawierać DUID Twojego TV i poziom uprawnień obejmujący manifest (`internet`). Utwórz go od nowa z właściwym DUID. |
| Aplikacja startuje i proces zaraz się restartuje | Awaria. Sprawdź, czy firmware to Tizen 9 i czy pakiet jest zbudowany jako `net6.0-tizen9.0`. |
| Wideo czarne, a dźwięk gra | WebView nie dekoduje H.264 na przetestowanym TV. Isko przekazuje strumienie do natywnego odtwarzacza ([opis](native-playback.md)). Strumień wymagający nagłówka `Referer` na razie nie zagra. |
