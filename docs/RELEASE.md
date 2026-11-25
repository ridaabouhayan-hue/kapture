# Kapture 0.3.5

Nieuw in 0.3.5:
- **Werkbalk weg tijdens het slepen**: zodra je een kader begint te slepen verdwijnt de werkbalk bovenin, zoals in het Knipprogramma van Windows. Laat je los zonder kader (te klein), dan komt hij terug. Bij de notitiebalk staat hij ook niet meer in beeld.

## Kapture 0.3.4

Nieuw in 0.3.4:
- **Print Screen werkt ook op Windows-versies die de toets niet doorgeven**: sommige versies van Windows 11 kopiëren bij Print Screen zelf het hele scherm naar het klembord en geven de toets niet aan Kapture. Kapture merkt dat nu aan het klembord (een afbeelding precies zo groot als je scherm) en opent dan het donkere scherm. Kapture kijkt alleen naar de maat van die afbeelding, niet naar wat erop staat, en luistert niet mee met je toetsenbord. Shift + PrtSc opent zo ook de notitiebalk. Windows-toets + PrtSc en Alt + PrtSc blijven van Windows.

## Kapture 0.3.3

Nieuw in 0.3.3:
- **Spoor in het foutlog om Print Screen uit te zoeken**: Kapture schrijft nu op welke sneltoetsen vastgelegd zijn, welke sneltoets binnenkomt, of het donkere scherm opent en waarom het weer dichtgaat. Er staan alleen toetsnamen en programmanamen in, nooit een screenshot, notitie, bestandsnaam of venstertitel.

## Kapture 0.3.2

Nieuw in 0.3.2:
- **Print Screen sluit niet meer meteen**: bij het loslaten van Print Screen haalde Windows (of het Knipprogramma) soms een eigen venster naar voren, waardoor Kapture het donkere scherm meteen weer sloot. Nu pakt Kapture in het eerste ogenblik de focus terug. In het foutlog staat welk programma het was (alleen de programmanaam).
- **Eigen sneltoets voor een screenshot**: in Instellingen, Sneltoetsen kies je een eigen toets, bijvoorbeeld Ctrl+Shift+K. Koppel die in Windows aan een tik met vier vingers op het touchpad: Instellingen, Bluetooth en apparaten, Touchpad, Geavanceerde bewegingen, Aangepaste snelkoppeling.

## Kapture 0.3.1

Nieuw in 0.3.1:
- **Editor soepeler**: tijdens het tekenen wordt alleen het nieuwe stukje opnieuw getekend, niet het hele venster. Pen, pijl, kader en verplaatsen volgen de muis nu direct.
- **Beter leesbaar**: blauwe knoppen en het gekozen gereedschap zijn kobalt met witte tekst, ook in donker (eerst was de tekst daar zwart).
- **Instellingen duidelijker**: witte kaarten op een grijze pagina, het gekozen onderdeel in het menu helder blauw met witte tekst, grotere koppen.
- **Nieuwe icoontjes** (Phosphor duotone): een vorm met een zachte blauwe vulling, beter herkenbaar in het menu, de editor en Instellingen.
- **Print Screen bezet?** Kapture probeert het nu zelf elke 15 seconden opnieuw en meldt het als het weer werkt. De uitleg noemt ook OneDrive (de instelling "Schermafbeeldingen opslaan in OneDrive" pakt Print Screen af).

## Kapture 0.3.0

Nieuw in 0.3: **de editor**.
- Open hem met **Bewerken** in de melding, of met een klik op de miniatuur. In Instellingen, Bewerken kies je of de miniatuur de editor van Kapture opent of het programma van Windows.
- Gereedschap bovenin: Selecteren, Pen, Markeerstift, Pijl, Rechthoek, Tekst, Nummer (1, 2, 3), Vervagen, Uitsnijden en Gum. Ongedaan maken en opnieuw (Ctrl+Z, Ctrl+Y).
- Rechts Eigenschappen: kleur (met een kleurkiezer), lijndikte, dekking en tekstgrootte. Met Selecteren gelden ze voor wat je gekozen hebt. Onderaan de notitie bij de screenshot.
- **Vervagen is echt**: de pixels worden vervangen, dus in het bestand is het daarna niet meer te lezen.
- **Klaar** bewaart de screenshot in hetzelfde bestand en zet hem op het klembord (met de notitiestrook). Opslaan en Kopiëren kunnen ook los.
- Sneltoetsen: V, P, M, A, R, T, N, B, C, E voor het gereedschap; Delete haalt weg wat je gekozen hebt.

## Kapture 0.2.2

Nieuw in 0.2.2:
- **Geen flits meer** voor het scherm donker wordt: de verschijn-animatie van Windows staat uit voor Kapture, en het stilstaande beeld wordt eerst getekend en dan pas getoond.
- **Menu bij de klok reageert meteen** als je met de muis over de regels gaat.
- In Instellingen is **Terug naar de standaardmap** weggehaald; met Wijzig kies je zelf een map.

Nieuw in 0.2.1:
- **Bijwerken met een klik**, zoals in NexusPen. Staat er een nieuwe versie op GitHub, dan zegt Kapture dat bij het starten (en in het menu bij de klok). Klik op Bijwerken: Kapture haalt hem op, controleert hem, installeert hem en start daarna vanzelf weer. Ook in Instellingen, Over en bijwerken: Nu zoeken.
- **Instellingen per pagina**, met onderaan **Opslaan** en **Annuleren**. Pas na Opslaan gebruikt Kapture de nieuwe instellingen, en je ziet een bevestiging. Sluit je met wijzigingen die nog niet bewaard zijn, dan vraagt Kapture of ze bewaard moeten worden.
- **Scherpere tekst**, net als in NexusPen.
- **Print Screen**: als Windows Print Screen nog naar het Knipprogramma stuurt, zegt Kapture dat en zet het met één klik (Zet uit) voor je uit.

## Ook in 0.2: notities, instellingen en een eigen map per maand

## Notities
- **In de melding** staat nu een notitieveld. Klik erop (of druk Ctrl+PrtSc) en typ waarom je de screenshot maakte. Enter bewaart, Shift+Enter is een nieuwe regel, Esc sluit.
- **Shift+PrtSc**: screenshot met notitie. Na het loslaten blijft het kader staan met een notitiebalk eronder. Je ziet meteen welke bestandsnaam er ontstaat. Esc = bewaren zonder notitie.
- **In de werkbalk** zit een knop Met notitie, voor als je toch een notitie wilt.
- **Ctrl+PrtSc**: notitie bij de laatste screenshot, ook als de melding al weg is.
- De notitie komt in de **bestandsnaam**, bijvoorbeeld `2026-11-24 0012 Chrome - Opslaan-knop werkt niet.png`. Is hij te lang of heeft hij meerdere regels, dan staat de hele tekst in een `.txt` ernaast.
- Op het **klembord** staat de notitie als strook onder de afbeelding, zodat Claude foto en reden ziet. Het bestand zelf blijft schoon.

## Instellingen
Open ze via het menu bij de klok, of start Kapture nog een keer. Met een zoekbalk bovenaan.
- **Algemeen**: thema (Systeem, Licht, Donker), starten met Windows, hoe lang de melding blijft staan.
- **Sneltoetsen**: elke sneltoets los aan of uit, alles pauzeren, en of Print Screen nog bij het Knipprogramma hoort.
- **Opslaan en mappen**: de inbox-map kiezen of openen, terug naar de standaardmap, en elke maand een eigen map (`Screenshots\2026-11`).
- **Notities**: de notitiestrook op het klembord en het tekstbestand aan of uit.

## Menu bij de klok
Nu zoals in het ontwerp: bovenaan Kapture met de preset, en Screenshot met notitie, Notitie bij laatste en Instellingen erbij.

Nog niet in deze versie: Laatste screenshots, Vrij en Venster, presets kiezen, de editor, talen.

Eerste keer installeren: Windows waarschuwt omdat Kapture geen betaalde handtekening heeft. Klik op Meer info en dan Toch uitvoeren. Bijwerken: installeer de nieuwe Kapture-Setup over de oude heen; je instellingen blijven staan.
