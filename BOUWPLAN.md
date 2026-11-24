# Kapture 1.0: bouwplan

Eigen screenshot-tool voor Windows 11, alleen voor Rida. Vervangt ShareX en het Windows Knipprogramma.
Dit document legt alle afspraken vast. Bij twijfel gaat dit bouwplan voor.

- Brief (bron van de afspraken): `docs/brief.md` (versie 24-11-2026)
- Ontwerp: de schermen en het logo komen van Google Stitch. Canvas met Stitch-schermen, licht en donker, met aanpassingen: https://claude.ai/artifact/ACNrAFzyd3t3EM22w6Zyg1
- Bron van het ontwerp in de repo: `docs/ontwerp/stitch/` (HTML per scherm, schermafdrukken licht en donker, `kapture_design_system.md`)

## Spelregels
- Pas bouwen als Rida "begin met bouwen" zegt. Grote keuzes eerst voorleggen.
- Stitch maakt het gezicht, Claude bouwt de motor. Claude ontwerpt geen schermen zelf; een nieuw scherm begint met een Stitch-ontwerp.
- Alle teksten in de app in het Nederlands, met taalkeuze (NL, EN, FR, ES). Nooit het lange gedachtestreepje.
- Kleine versies. Na elke versie: GitHub Actions groen, daarna een aparte code-review (veiligheid, stabiliteit, snelheid) voordat Rida hem krijgt.
- ShareX is GPL-3.0: we kopieren er geen regel code uit. Alles zelf geschreven.

## Techniek
- C# 5 met WinForms op .NET Framework 4.8 (zit in Windows 10 en 11). Doel: klein programma, weinig geheugen, niets doen in rust.
- Zelf getekende vensters met UpdateLayeredWindow (werkbalk, melding, notitiebalk, tray-menu). Editor, Instellingen en Laatste screenshots zijn gewone vensters, ook zelf getekend.
- PerMonitorV2 (juiste schaal per scherm). Coordinaten altijd in fysieke pixels van het virtuele scherm.
- Sneltoetsen alleen via RegisterHotKey. Geen toetsenbordhooks.
- Schermafbeelding met GDI (BitBlt van het bureaublad). Schermopname (video) later: dan Windows.Graphics.Capture en mogelijk een nieuwere .NET. Het ontwerp houdt daar ruimte voor (de vorm "Opname" past als vijfde knop in de werkbalk).
- Gaat zelf niet het internet op, behalve de optionele check op een nieuwe versie (GitHub Releases).
- Lettertypes: voor nu Segoe UI en Cascadia Mono, die in Windows zitten (geen download nodig), net als NexusPen. Tekst op een dichte achtergrond met ClearType, zodat hij scherp is. Niet Segoe UI Variable: die gaf gekartelde letters (Rida 24-11). Inter en JetBrains Mono uit het Stitch-ontwerp komen erbij zodra Rida het downloaden goedkeurt. Icoontjes: Phosphor duotone (MIT): een vorm met een zachte kobalt vulling, gekozen door Rida (24-11) uit vier voorbeelden. Lucide (ISC) blijft als reserve.
- Licenties van Phosphor, Lucide, Inter en JetBrains Mono meeleveren.

### Hergebruik uit NexusPen
Overnemen en aanpassen (niet als gedeelde bibliotheek, maar als kopie in deze repo):
- `Win32.cs`, `Log.cs`, `Perf.cs`: vensters, foutlog, meten.
- `Svg.cs`, `Icons.cs`, `IconData.cs`, `tools/maak_icoontjes.py`: Lucide-iconen.
- `Theme.cs`: licht en donker, nu met de Kapture-kleuren.
- `Lang.cs`, `LangData.cs`, `tools/check_vertalingen.py`: talen.
- `Settings.cs`, `Shortcuts.cs`: instellingen bewaren, sneltoetsen lezen en controleren op dubbel.
- `Update.cs`: check op een nieuwe versie (adres wordt de Kapture-repo).
- `Usage.cs`: gebruik bijhouden, alleen lokaal.
- `MenuPopup.cs`: zelf getekend rechtsklikmenu (basis voor het tray-menu).
- `Render.cs`, `TextLayout.cs`, `TextEditor.cs`, `Model.cs`: de tekenmotor voor de editor (pen, markeerstift, pijl, vormen, tekst met rand, nummerstempel, gum, selecteren).
- `build/build.ps1`, `installer/*.iss`, `.github/workflows/build.yml`: bouwen, testen, installer, automatische release.

## Ontwerp (uit Stitch)
- Kleuren: licht accent `#2563EB`, donker accent `#3B82F6`. Neutrale kleuren uit `kapture_design_system.md`. Nergens groen.
- Blauwe vlakken (knoppen, gekozen gereedschap, schakelaars, gekozen menu-item) zijn altijd kobalt met witte tekst, ook in donker. Lichtblauw met donkere tekst was slecht te lezen (Rida 24-11).
- In Instellingen staan witte kaarten op een grijzere pagina (in donker: donkerder pagina), zodat niet alles hetzelfde lijkt.
- Hoeken 8 tot 12 px, knopjes en chips 6 tot 8 px. Raster van 4 en 8 px.
- Het matglas van Stitch wordt een effen vlak in dezelfde kleur (snelheid, en glas werkt slecht met layered windows). Mica alleen bij Editor en Instellingen als de spike laat zien dat het werkt.
- Beweging: wisselen 100 ms, verschijnen 200 ms, verdwijnen 150 ms. Niets stuitert.
- Selectiekader: een gestippelde rand (streepjes, zoals het Windows Knipprogramma), geen dikke lijn. De handvatten blijven. (Rida 24-11)
- Presets in Instellingen: de Stitch-vorm voor nu. Rida vond de kaartjes uit Claudes schets ook mooi; misschien later.
- Bij twijfel: het Stitch-scherm gaat voor op dit bouwplan als het om uiterlijk gaat; dit bouwplan gaat voor als het om gedrag gaat.

## Logo
- Stitch-logo "Kader-K" (cobalt 5a.1): een K met een kader en vier handvatten in een afgeronde tegel.
- 48 px en groter: de Stitch-vorm (witte tegel, kobalt kader en K). Lijnen dikker naarmate het icoon kleiner wordt.
- 32 px en kleiner: alleen de K, wit op een kobalt tegel. Kader en handvatten vallen weg (onleesbaar en verdwijnt op een lichte taakbalk).
- Pakket: SVG, PNG (16, 20, 24, 32, 40, 48, 64, 96, 128, 256, 512, 1024) en een ICO met 16 tot 256.
- Sneltoetsen op pauze: tray-icoon met een grijs streepje.

## Kernflow
1. Sneltoets. Kapture maakt eerst een stilstaand beeld van het scherm, toont dat iets donkerder, en bovenaan de werkbalk: Kader, Vrij, Venster, Hele scherm, de knop Met notitie, de preset, sluiten.
2. Kader trekken en loslaten: de overlay is meteen weg. Op dat moment is de afbeelding al opgeslagen en gekopieerd (volgens de preset).
3. Rechtsonder de melding: miniatuur, "Opgeslagen en gekopieerd", de preset, een notitieveld, Bewerken, Kopieer opnieuw, Map openen, Verwijderen, sluiten. Pakt het toetsenbord niet af (WS_EX_NOACTIVATE). Blijft 6 seconden (instelbaar), blijft staan zolang de muis erboven hangt. Klik of Ctrl+PrtSc geeft het notitieveld het toetsenbord.
4. Paneel Laatste screenshots (tray of Ctrl+Shift+PrtSc): de laatste 10. Per rij: notitie typen of wijzigen (klik op de tekst), Bewerken, Kopieer opnieuw, Map openen, Verwijderen. Bovenaan Inbox openen.
5. Shift+PrtSc "met notitie": na loslaten blijft het kader staan met de notitiebalk eronder ("Waarom maak je deze screenshot?", "Al opgeslagen en gekopieerd", Enter). Onder de balk de bestandsnaam die ontstaat. Esc = bewaren zonder notitie. Past de balk niet onder het kader, dan erboven.

## Architectuur (na de review van 24-11-2026)
Eén proces, één berichtenlus. De onderdelen en hoe een screenshot erdoorheen gaat:

```
PrtSc -> Sneltoetsen -> Vastleggen (variant C) -> Overlay (kader trekken)
                                                  |
                       loslaten: uitsnede ------->+--> Klembord (STA-draad, met notitiestrook)
                                                  +--> Opslaan (achtergronddraad: PNG, naam, OneDrive)
                                                  +--> Melding -> Notitie -> Hernoemen
                                                  +--> Geschiedenis (laatste 10)
```
- **Een preset is een lijst stappen (gegevens), geen code.** Eigen presets zijn dan alleen een nieuwe lijst. Stappen: opslaan, opslaan in map, klembord afbeelding, klembord bestand, editor eerst, melding, notitie vragen.
- **Eén Kapture tegelijk:** een Mutex. Start je hem nog eens, dan opent het bestaande Kapture zijn paneel in plaats van dubbele sneltoetsen.
- **Sneltoets bezet** (bijvoorbeeld door ShareX die nog draait, of het Knipprogramma): Kapture zegt welke toets niet lukt en door wie dat waarschijnlijk komt, en werkt verder met de andere toetsen. ShareX moet uit voordat Kapture PrtSc krijgt.
- **Schermgrootte per keer opnieuw bepalen,** niet bij het opstarten: een laptop kan van scherm of schaal wisselen.
- **Veilig opslaan:** eerst naar `naam.png.tmp` in dezelfde map, dan hernoemen. Zo ziet OneDrive nooit een half bestand. Hetzelfde voor `geschiedenis.json` en de instellingen.
- **Klembord:** zowel PNG als gewone afbeelding (DIB) erop, zodat plakken werkt in Claude, de browser, Word en WhatsApp. Lukt het klembord niet (een ander programma houdt het vast), dan tot 5 keer opnieuw binnen een halve seconde, daarna een melding.
- **De overlay mag het scherm nooit blokkeren:** Esc werkt altijd, en een waakhond sluit de overlay als Kapture 3 seconden niet reageert (zoals in NexusPen).
- **Vastleggen achter een vaste ingang** (`ICapture`), zodat later vastleggen via de videokaart of schermopname erbij kan zonder de rest te veranderen.
- **Editor:** ongedaan maken bewaart stappen, geen hele afbeeldingen, zodat een 4K-screenshot niet honderden MB's kost. Vervagen schrijft echte pixels weg.
- **Vergrendeld scherm of beveiligd bureaublad** (UAC-venster): vastleggen kan daar niet; Kapture doet dan niets en logt alleen "niet mogelijk".

## Sneltoetsen (standaard, allemaal instelbaar en los uit te zetten)
| Toets | Doet |
|---|---|
| PrtSc | screenshot met de actieve preset |
| Shift+PrtSc | screenshot met notitie |
| Ctrl+PrtSc | notitie bij de laatste screenshot |
| Ctrl+Shift+PrtSc | paneel Laatste screenshots |
| In de overlay: 1 tot 4 | preset kiezen |
| In de overlay: Esc | annuleren |
Daarnaast een eigen sneltoets voor een screenshot (Instellingen, Sneltoetsen; bijvoorbeeld Ctrl+Shift+K; geen Ctrl+letter of Ctrl+Alt+letter, want die hebben andere programma's en AltGr nodig), te koppelen aan een touchpad-gebaar in Windows (Rida 24-11). Win+Shift+S blijft van Windows. Rida zet in Windows eenmalig "Print Screen opent Knipprogramma" uit; het eerste-keer-scherm helpt daarbij (knop naar de instelling, en controle of PrtSc vrij is).

## Notities
- Het opgeslagen bestand blijft schoon. Er wordt nooit iets op de afbeelding geschreven.
- Bestandsnaam: `jjjj-mm-dd uumm Venster - Notitie.png`, bijvoorbeeld `2026-11-24 0012 Chrome - Opslaan-knop werkt niet.png`. Het patroon is instelbaar.
- Tekens die Windows niet toestaat (`\ / : * ? " < > |`) worden weggehaald; regeleinden worden een spatie. De naam is hooguit 120 tekens. Past de notitie niet, dan wordt hij ingekort in de naam en komt de volledige tekst in `<zelfde naam>.txt` ernaast.
- Bestaat de naam al, dan komt er ` (2)` achter.
- Een notitie achteraf hernoemt het bestand (en het tekstbestand). Lukt hernoemen niet omdat OneDrive het bestand vasthoudt, dan opnieuw proberen (3 keer, oplopend tot 2 s) en anders een melding met "Opnieuw".
- Klembord (Nu naar Claude, Bewaren voor Claude, Kies zelf): de gekopieerde afbeelding krijgt onderaan een strook met de notitie. Het bestand blijft schoon. Zonder notitie geen strook.
- Doorsturen: de notitie gaat nooit mee, niet op het klembord en niet in het bestand dat je deelt.
- Geschiedenis van de laatste screenshots: `%APPDATA%\Kapture\geschiedenis.json` (pad, tijd, venster, preset, notitie). Alleen op deze pc.

## Opslaan
- Inbox: `%OneDrive%\Pictures\Screenshots` (voorstel). Bestaat OneDrive niet, dan `Pictures\Screenshots` van Windows.
- Let op: het Windows Knipprogramma bewaart standaard ook in Pictures\Screenshots. Zolang Kapture het Knipprogramma vervangt, is dat juist handig: alles op een plek.
- Elke maand een eigen map (aan of uit in Instellingen, standaard uit): `Screenshots\2026-11`. Inbox-map openen opent altijd de inbox zelf.
- Kan Kapture niet in de inbox schrijven (OneDrive uit, schijf weg), dan zegt hij dat bij het opstarten en bij het opslaan, met een knop naar Instellingen.
- Opslaan in...: recente mappen (laatste 8) en favorieten (zelf vastzetten).
- Formaat: PNG. JPG later als optie.

## Presets
| Preset | Wat er gebeurt |
|---|---|
| Nu naar Claude (standaard) | klembord (met notitiestrook als er een notitie is) en opslaan in de inbox; melding met notitieveld |
| Bewaren voor Claude | opslaan in de inbox met datum en venstertitel, klembord blijft ongemoeid; notitie mag |
| Doorsturen | eerst de editor (pijl, nummer, vervagen); daarna Kopieer als afbeelding of Kopieer als bestand; nooit de notitie |
| Kies zelf | niets vanzelf; alleen de melding met knoppen |
| Opslaan in... | kiezen uit recente en favoriete mappen |
Eigen presets maken kan vanaf 1.0 (stappen aan en uit, zoals in Instellingen > Presets).

## Editor (eerste versie)
- Uit NexusPen: selecteren, pen, markeerstift, pijl, rechthoek en cirkel, tekst met rand, nummerstempel (1, 2, 3), gum, kleuren, dikte.
- Nieuw: vervagen of pixeleren (verplicht, voor privacy), uitsnijden, kleurkiezer (pipet), ongedaan en opnieuw, dekking.
- Rechts het paneel Eigenschappen en onderaan de notitie. Bij Doorsturen is het notitievak grijs.
- Knoppen: Kopieren, Opslaan, Klaar. Bij Doorsturen: Kopieer als afbeelding, Kopieer als bestand.
- Vervagen is echt: de pixels worden vervangen, niet een laag erover die je kunt weghalen.

## Instellingen (met zoekbalk)
Menu: Algemeen, Sneltoetsen, Presets, Opslaan, Notities, Bewerken, Taal, Gebruik, Over en updates.
- De zoekbalk zoekt in de naam en uitleg van elke instelling en springt ernaartoe.
- Thema: Systeem, Licht, Donker. Accentkleur: eerst alleen kobalt; andere kleuren (ook voor het logo) later.

## Tray-menu
Kapture (met de actieve preset), Nieuwe screenshot (PrtSc), Screenshot met notitie (Shift+PrtSc), Notitie bij laatste (Ctrl+PrtSc), Laatste screenshots, Presets, Inbox-map openen, Instellingen, Sneltoetsen pauzeren, Afsluiten.

## Van ShareX
Uit de brief, zo afgesproken:
- **Houden:** kopieer afbeelding, opslaan en Opslaan in..., Bewerken, kopieer bestand en bestandspad, Map openen, Laatste screenshots, presets, Screenshots-map, instellingen en sneltoetsen met zoekbalk.
- **Tweede fase:** tekst herkennen (OCR) met de tekst als bestand naast de screenshot (Windows.Media.Ocr, werkt zonder internet).
- **Later:** vastpinnen op het scherm, Beautify, watermerk, QR-code scannen, liniaal, scrollende capture, schermopname.
- **Verstoppen (geavanceerd):** externe programma's starten, taakinstellingen.
- **Schrappen:** quick task menu, After capture-venster, afdrukken, miniatuur opslaan, mappad kopieren, Analyze image, Before upload, alles met uploaden, Verwijder lokaal bestand (verwijderen alleen via Laatste screenshots), losse afbeeldingsgeschiedenis, Debug, Doneer, Follow, Discord.

## Privacy
- Screenshots bevatten vaak persoonlijke gegevens (bank, inkomen, cursisten, WhatsApp). Er gaat nooit iets vanzelf naar internet.
- Het gebruikslog bevat nooit de inhoud van een screenshot, een notitie, een bestandsnaam of een venstertitel. Wel: welke knop, welke preset, hoe lang de overlay openstond, hoe snel het ging.
- Het foutlog bevat geen bestandsnamen of notities.

## Gebruik bijhouden
- Alleen op deze pc (`%APPDATA%\Kapture\gebruik`), per onderdeel aan of uit.
- Meten: welke sneltoets en preset, vorm (kader, vrij, venster, scherm), hoe vaak een notitie (direct, achteraf, nooit), editor-gereedschap, snelheid (sneltoets tot overlay, loslaten tot opgeslagen), fouten.
- Knop "Exporteren voor Claude".

## Spike (eerst, voor de eerste versie)
Een klein los testprogramma, geen onderdeel van de app. Meet en rapporteert:
1. **Snelheid:** van WM_HOTKEY tot de overlay op het scherm staat. Doel onder 100 ms, ook op 4K en 150%. Aanpak: het overlayvenster bij het opstarten al maken en verborgen houden; bij de sneltoets alleen BitBlt van het scherm en tonen. Gemeten met een tijdstempel bij WM_HOTKEY en na UpdateLayeredWindow plus DwmFlush, 50 keer, p50 en p95.
2. **Geheugen in rust:** werkset en private bytes na 1 minuut niets, na 20 screenshots, en 5 minuten daarna. Doel: in rust onder 40 MB private bytes, en na 20 screenshots weer terug in de buurt van rust.
3. **Print Screen overnemen:** RegisterHotKey op PrtSc (VK_SNAPSHOT) met de Windows-instelling aan en uit. Wat gebeurt er als de instelling aan staat? Kunnen we dat herkennen (register `HKCU\Control Panel\Keyboard\PrintScreenKeyForSnippingEnabled`) en netjes melden?
4. **Gemengde schaal:** kader trekken op 100%, 125%, 150% en 175%, en over twee schermen met verschillende schaal. Klopt elke pixel van het kader met de opgeslagen afbeelding?
5. **Mica:** werkt de Windows 11-achtergrond bij een gewoon venster (Editor, Instellingen) samen met ons eigen tekenen?
Resultaat: `docs/SPIKE.md` (ronde 2, 24-11-2026). Gekozen: eerst een donker venster dat onzichtbaar is voor de opname (donker na 12 ms), dan vastleggen (stilstaand beeld na 90 tot 140 ms op 4K); schermbuffer op aanvraag en daarna weg; PNG opslaan op de achtergrond; in rust 23 MB.

## Versies
| Versie | Wat erin zit |
|---|---|
| 0.1 | Repo, build, tests, installer, automatische release. Tray-icoon, PrtSc, overlay met kader, opslaan in de inbox, klembord, eenvoudige melding. |
| 0.2 | Notities: notitieveld in de melding, Shift+PrtSc met notitiebalk, Ctrl+PrtSc, bestandsnaam en tekstbestand, notitiestrook op het klembord. Op verzoek van Rida (24-11) ook al Instellingen met zoekbalk (Algemeen, Sneltoetsen, Opslaan en mappen, Notities, Over) en een map per maand. |
| 0.2.1 | Bijwerken met een klik (uit NexusPen), Instellingen per pagina met Opslaan en bevestiging, scherpe tekst (Segoe UI met ClearType), Print Screen losmaken van het Knipprogramma met een klik. |
| 0.3 | Editor (op verzoek van Rida naar voren gehaald, 24-11): tekenstijl uit NexusPen (pen, markeerstift, pijl, rechthoek, tekst met rand, nummer), echt vervagen, uitsnijden, kleurkiezer, ongedaan en opnieuw. Bewerken in de melding; miniatuur opent de editor of Windows (instelling). |
| 0.4 | Laatste screenshots en het volledige tray-menu. Hernoemen achteraf. |
| 0.5 | Vormen Vrij, Venster, Hele scherm. Presets en Opslaan in... Doorsturen (editor eerst, zonder notitie). Editor uitbreiden (cirkel, lijn, selecteren met een kader). |
| 0.6 | Instellingen aanvullen (Presets, Bewerken, Taal, Gebruik), talen, eerste-keer-scherm, gebruik bijhouden. |
| 1.0 | Alles nagelopen, logo-pakket, eigen presets, code-review. |
| 1.x | OCR, dan de lijst "Later". |

## Installeren
- Kapture-Setup.exe, zonder beheerdersrechten, in `%LOCALAPPDATA%\Programs\Kapture`.
- Snelkoppeling in Start (en desgewenst op het bureaublad), starten met Windows, icoon bij de klok.
- Bijwerken met dezelfde setup; instellingen, geschiedenis en presets blijven staan.
- Zonder betaalde handtekening geeft Windows de eerste keer een waarschuwing: Meer info > Toch uitvoeren.

## Open punten (voor Rida)
- Repo openbaar of privé. Update-check via GitHub Releases werkt alleen zonder inloggen als de repo openbaar is. Advies: openbaar (de code bevat niets persoonlijks), of privé en zelf bijwerken met de setup.
- Naam: Kapture is de werknaam (bestaat al als Mac-app Kapture+). Geen probleem voor eigen gebruik.
