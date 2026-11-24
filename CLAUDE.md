# Kapture: werkafspraken voor Claude Code

Kapture is een eigen screenshot-tool voor Windows 11 van Rida Abouhayan (NT2-docent), alleen voor eigen gebruik. Hij vervangt ShareX en het Windows Knipprogramma. Het bijzondere: bij elke screenshot snel noteren waarom je hem maakte, zonder op de foto te schrijven.

## Altijd eerst lezen
1. `BOUWPLAN.md`: alle afgesproken keuzes. Dit gaat voor bij twijfel over gedrag.
2. `docs/ontwerp/stitch/`: het ontwerp uit Google Stitch (HTML per scherm, schermafdrukken licht en donker, `kapture_design_system.md`). Dit gaat voor bij twijfel over uiterlijk.
3. `docs/brief.md`: de oorspronkelijke brief, voor het waarom.

## Werkafspraken met Rida
- Schrijf in het Nederlands. Gebruik nooit het lange gedachtestreepje, ook niet in code-commentaar, teksten of commits.
- Begin een bericht nooit met zijn naam.
- Begin pas met bouwen als Rida "begin met bouwen" zegt. Vraag eerst voordat je iets groots verandert.
- Rida is geen programmeur. Leg kort en in gewone woorden uit wat je doet en wat hij moet doen: wat, waarom, genummerde stappen, en hoe hij ziet dat het lukte.
- Neem zelf de technische beslissingen en vraag alleen wat alleen Rida kan weten.
- Stitch maakt het gezicht, Claude de motor. Ontwerp geen schermen zelf. Een nieuw of ander scherm begint met een Stitch-ontwerp; jij vertaalt het zo precies mogelijk naar code en zegt wat er technisch anders moet.
- Meten, niet aannemen. Een scherm is niet af tot je er een schermafdruk van hebt gezien, licht en donker, op 100% en 150%.
- Zeg altijd wat er op een screenshot of bestand staat voordat je iets deelt.

## Techniek
- C# 5 met WinForms op .NET Framework 4.8. Er hoeft niets extra geïnstalleerd te worden.
- Code in `src/`, opgesplitst per onderdeel. Veel komt uit NexusPen (zie BOUWPLAN.md, Hergebruik): kopieer en pas aan, houd het los van NexusPen.
- Zwevende vensters (werkbalk, melding, notitiebalk, tray-menu) tekenen zichzelf met UpdateLayeredWindow. Icoontjes op hele pixels.
- PerMonitorV2. Sneltoetsen alleen via RegisterHotKey, nooit een toetsenbordhook.
- Icoontjes: Phosphor duotone (MIT, keuze van Rida 24-11), paden letterlijk uit @phosphor-icons/core, omgezet met tools/maak_phosphor.py naar src/PhosphorData.cs. Lucide (ISC) blijft als reserve in src/IconData.cs. Lettertypes: voor nu Segoe UI en Cascadia Mono (zitten in Windows), net als NexusPen; tekst op een dichte achtergrond met ClearType (TextRenderer) zodat hij scherp is. Niet Segoe UI Variable: die tekent gekarteld. Inter en JetBrains Mono uit Stitch komen erbij als Rida het downloaden goedkeurt.
- Logo: `assets/logo/` en `src/Logo.cs`. Groot = Stitch-vorm, 32 px en kleiner = witte K op kobalt.
- Tests: `tests/Tests.cs`. De zelftest (`--zelftest`) maakt schermafdrukken van elk venster in licht en donker.
- ShareX is GPL-3.0. Kopieer er nooit code uit, ook geen stukje. Idee mag, code niet.

## Privacy
- Er gaat nooit iets vanzelf naar internet, behalve de optionele check op een nieuwe versie.
- Nooit in een log: de inhoud van een screenshot, een notitie, een bestandsnaam of een venstertitel.
- Testafbeeldingen in de repo zijn altijd nep (geen echte mails, WhatsApp, cursisten of bankgegevens).

## GitHub
- Repository `kapture` (openbaar of privé: zie BOUWPLAN.md, Open punten).
- GitHub Actions op `windows-latest`: bouwen, tests, zelftest met schermafdrukken, installer (Kapture-Setup.exe met Inno Setup, zonder beheerdersrechten, in %LOCALAPPDATA%\Programs\Kapture).
- Release gaat vanzelf: staat er een nieuw versienummer op main (`Version` in `src/Program.cs`) en is alles groen, dan maakt GitHub Actions tag vX.Y.Z en een Release met Kapture-Setup.exe en Kapture.exe, met `docs/RELEASE.md` als tekst. Verhoog het versienummer pas als een versie klaar en nagekeken is.
- Na elke versie: een aparte code-review (veiligheid, stabiliteit, snelheid) voordat Rida hem krijgt.
- Acties in de workflow op een vaste commit, zoals in NexusPen.

## Gebruik
- Gebruiksgegevens alleen lokaal (`%APPDATA%\Kapture\gebruik`). Knop "Exporteren voor Claude".
