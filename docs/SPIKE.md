# Spike: meten voor we bouwen (24-11-2026, ronde 2)

Een los testprogramma (`spike/Spike.cs`), geen onderdeel van de app. Gedraaid op Rida's pc: Windows 11 (build 26300), één scherm van 3840x2160 op 150%, .NET Framework 4.8. Het programma bewaart geen schermafbeelding op schijf en raakt het klembord niet aan. Ruwe uitslagen: `spike/resultaat-B-1.txt`, `-B-2`, `-C-1`, `-C-2`.

Ronde 2 herstelt de meetfouten uit de code review van ronde 1: de pixelcontrole zat in de gemeten tijd, een te laat sneltoetsbericht kon meetellen, en fouten van Windows werden niet gecontroleerd. De getallen hieronder zijn schoon gemeten, elke variant twee keer 30 rondes.

## Uitslag in het kort
| Vraag | Doel | Gemeten | Oordeel |
|---|---|---|---|
| Sneltoets tot scherm wordt donker | onder 100 ms | **11 tot 12 ms** (p95 14 ms) | ruim gehaald |
| Sneltoets tot stilstaand beeld klaar | onder 100 ms | **90 tot 137 ms** (p50), wisselt met hoe druk de pc is | net niet altijd, zie hieronder |
| Geheugen in rust | onder 40 MB | **22,6 MB** privé; na een screenshot 28 MB privé en 4 MB werkset | gehaald |
| Print Screen overnemen | lukt | PrtSc, Shift+, Ctrl+ en Ctrl+Shift+PrtSc alle vier vast te leggen | lukt, met een kanttekening |
| Schaal en pixels | elke pixel klopt | 145 tot 150 van 150 punten gelijk, op 150% en 4K | gehaald |
| Donker venster niet in de foto | niet zichtbaar | 150 van 150 punten gelijk in variant C | gehaald |
| Gemengde schaal (twee schermen) | klopt | niet getest: er is maar één scherm | open |
| Mica voor Editor en Instellingen | werkt | Windows accepteert het; niet bekeken | open tot de Editor |
| Mislukte rondes | 0 | 0 mislukt, 0 niet aangekomen, in alle 120 rondes | gehaald |

## 1. Snelheid
| Variant | Hoe | Donker scherm | Stilstaand beeld klaar (p50 / p95) |
|---|---|---|---|
| A (ronde 1) | vastleggen met CAPTUREBLT, pixels zelf donker maken | pas met het beeld | 117 tot 173 / 148 tot 188 ms |
| B | vastleggen, dan beeld plus apart donker venster tonen | pas met het beeld | 90 tot 120 / 114 tot 168 ms |
| C | eerst het donkere venster (onzichtbaar voor de opname), dan vastleggen | **11 tot 12 ms** | 101 tot 137 / 123 tot 162 ms |

- Het grootste stuk is het vastleggen van een 4K-scherm door Windows: 66 tot 97 ms, afhankelijk van wat de pc verder doet. Alles van Kapture zelf (tonen, wachten op het scherm) is samen ongeveer 30 ms.
- **Keuze: variant C.** Het scherm wordt binnen 12 ms donker: dat voelt direct, want je ogen merken pas iets vanaf ongeveer 100 ms. Het stilstaande beeld komt 90 tot 140 ms na de toets. Een mens begint pas na ongeveer 200 ms met slepen, dus je kunt nooit een kader trekken voordat het beeld klaar is.
- Eerlijk: het oorspronkelijke doel "sneltoets tot selectie onder 100 ms" haalt Kapture voor wat je ziet (12 ms), maar niet altijd voor het vastleggen zelf op 4K. Wordt dat ooit merkbaar, dan is de volgende stap vastleggen via de videokaart (DXGI Desktop Duplication, naar verwachting 10 tot 20 ms). Dat is meer werk en nu niet nodig.
- Opslaan als PNG van een volledig 4K-scherm kost 160 tot 270 ms. Dat gebeurt na het loslaten, op de achtergrond; de overlay is dan al weg. Een kader is kleiner en dus sneller.

## 2. Geheugen
| Moment | Privé | Werkset |
|---|---|---|
| In rust, net gestart | 22,6 MB | 24,7 MB |
| Laatste schermbuffer nog vast | 59,7 MB | 124 MB |
| Buffer weg, werkset ingekrompen | 28 MB | 3,6 MB |
| 5 seconden later | 28 MB | 4,2 MB |

- Een schermbuffer voor 4K kost 32 MB. Hem maken kost vrijwel niets (ronde 1: 0,0 ms). **Keuze:** buffer pas maken bij de sneltoets en na het opslaan weggooien, daarna de werkset inkrimpen.

## 3. Print Screen
- De Windows-instelling "Print Screen opent Knipprogramma" staat niet expliciet in het register; op Windows 11 betekent dat: aan.
- Toch kon de spike alle vier de Print Screen-sneltoetsen vastleggen. Wat er gebeurt als je echt op PrtSc drukt (krijgt Kapture hem, het Knipprogramma of allebei), is niet getest: dan zou het Knipprogramma op je scherm opengaan.
- **Keuze:** het eerste-keer-scherm leest de instelling en stuurt je naar de Windows-instelling als hij aan staat. Bij versie 0.1 test Rida één keer met de hand: PrtSc met de instelling aan en uit. ShareX moet dan uit staan.

## 4. Schaal en pixels
- Per-scherm-schaal (PerMonitorV2) werkt: het programma ziet 3840x2160 echte pixels, niet 2560x1440.
- De pixels in de foto zijn gelijk aan het scherm van vlak voor de toets (C: 300 van 300; B: 295 van 300, het verschil kwam door iets dat in die tijd veranderde, zoals de klok).
- Twee schermen met verschillende schaal: niet getest, er is één scherm.

## 5. Mica
- Windows accepteert Mica en een donkere titelbalk voor gewone vensters. Of het er goed uitziet met ons eigen tekenen, bekijk ik bij de Editor (versie 0.5).

## Wat de test niet afdekt
- De test drukt zelf een toets in (Ctrl+Alt+Shift+F11). Een echte PrtSc-druk kan een paar ms anders zijn.
- Alleen deze pc, alleen 4K op 150%. Een laptop met een kleiner scherm is sneller (minder pixels).

## Wat dit betekent voor het bouwplan
- De overlay werkt volgens variant C: eerst donker (onzichtbaar voor de opname), dan vastleggen, dan het stilstaande beeld eronder.
- Schermbuffer op aanvraag, daarna weg; werkset inkrimpen na elke screenshot.
- PNG opslaan op de achtergrond na het loslaten.
- Vastleggen achter een vaste ingang, zodat DXGI later kan.
- Eén handmatige test bij 0.1: Print Screen met de Windows-instelling aan en uit.
