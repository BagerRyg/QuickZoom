# QuickZoom – brugervejledning

**Til QuickZoom 3.3.54, build 355 · Windows 10/11, 64-bit**

[Read the manual in English](QuickZoom-User-Manual-English.md)

Vi har udviklet QuickZoom for at gøre lille tekst, knapper og andre detaljer på skærmen lettere at se. QuickZoom forstørrer det, du ser på skærmen, uden at ændre skriftstørrelsen eller layoutet i dine gemte dokumenter. Du kan forstørre hele skærmen, bruge en bevægelig linse eller vise et forstørret udsnit langs skærmens kant.

Denne vejledning beskriver funktionerne i build 355. En ældre udgave kan se anderledes ud. Du finder din version under **Åbn indstillinger → Om**.

## Indhold

1. [Start her: forstør skærmen, og vend tilbage til normal størrelse](#1-start-her-forstør-skærmen-og-vend-tilbage-til-normal-størrelse)
2. [Gør QuickZoom klar](#2-gør-quickzoom-klar)
3. [Første opsætning trin for trin](#3-første-opsætning-trin-for-trin)
4. [Find og brug menuen ved uret](#4-find-og-brug-menuen-ved-uret)
5. [Vælg, hvordan skærmen skal forstørres](#5-vælg-hvordan-skærmen-skal-forstørres)
6. [Vælg, hvad visningen skal følge](#6-vælg-hvad-visningen-skal-følge)
7. [Alle genveje til daglig brug](#7-alle-genveje-til-daglig-brug)
8. [Brug indstillingsvinduet](#8-brug-indstillingsvinduet)
9. [Generelle indstillinger](#9-generelle-indstillinger)
10. [Zoomindstillinger](#10-zoomindstillinger)
11. [Skærmindstillinger](#11-skærmindstillinger)
12. [Museindstillinger](#12-museindstillinger)
13. [Genvejsindstillinger](#13-genvejsindstillinger)
14. [Indstillinger for udseende](#14-indstillinger-for-udseende)
15. [Om, automatisk start og privatliv](#15-om-automatisk-start-og-privatliv)
16. [Eksempler fra hverdagen](#16-eksempler-fra-hverdagen)
17. [Fejlfinding](#17-fejlfinding)
18. [Gem, sikkerhedskopiér, nulstil, opdatér eller fjern QuickZoom](#18-gem-sikkerhedskopiér-nulstil-opdatér-eller-fjern-quickzoom)

## 1. Start her: forstør skærmen, og vend tilbage til normal størrelse

Her går vi ud fra, at du har gennemført opsætningen og beholdt **Alt** som aktiveringstast. Har du valgt en anden tast, skal du bruge den, hver gang vejledningen nævner Alt. På et dansk tastatur skal du bruge venstre Alt, ikke AltGr.

### Gør noget større

1. Flyt musemarkøren hen til den tekst eller genstand, du vil se nærmere på.
2. Hold **Alt** nede, og rul musehjulet opad. Du kan også holde **Alt** nede og trykke på tastaturets **+**-tast.
3. Slip Alt, når visningen er stor nok. Forstørrelsen bliver stående; du behøver ikke holde tasten nede.
4. Flyt musen for at se dig omkring i det forstørrede område. Med standardvalget **Automatisk** kan visningen også følge din indtastning og navigation med tastaturet.

**100 %** er normal størrelse. **200 %** er dobbelt størrelse. Som standard ændrer hvert zoomtrin forstørrelsen med 30 procentpoint: 100 %, 130 %, 160 % og så videre op til den valgte grænse.

Sker der ikke noget, skal du åbne QuickZooms menu ved uret og kontrollere, at **Forstørrelse** står på **Til**. Det gør zoomgenvejene aktive; du skal stadig selv zoome ind.

### Vend tilbage til normal størrelse

- Hold **Alt** nede, og rul nedad, eller tryk på **Alt + −**, indtil du når 100 %.
- Du kan også åbne menuen ved uret og sætte **Forstørrelse** til **Fra**. Det sætter straks zoomniveauet tilbage til 100 %.
- Ser farverne stadig omvendte ud, skal du også sætte **Inverterede farver** til **Fra** i menuen.
- Vil du stoppe QuickZoom helt, skal du vælge **Afslut** og derefter **Er du sikker?** i samme menu.

Denne version har ingen særskilt global genvej til at nulstille zoom eller afslutte i en nødsituation. **Escape** lukker normalt QuickZooms menuer eller indstillingsvindue; den ophæver ikke skærmforstørrelsen. QuickZoom bliver også ved med at køre, når du lukker indstillingerne.

## 2. Gør QuickZoom klar

### Det skal du bruge

- En pc med **Windows 10 eller Windows 11, 64-bit (x64)**.
- Et tastatur. En mus med rullehjul er praktisk, men er ikke nødvendig for at zoome.
- En mappe på pc'ens **lokale, faste drev**, for eksempel en almindelig mappe på C:.
- Tilladelse til at gemme indstillinger i din Windows-brugers lokale mappe til programdata.

Den selvstændige udgivelse indeholder de nødvendige .NET-programfiler, så du behøver ikke installere .NET særskilt. En udviklerudgave, der ikke er selvstændig, kræver .NET 10 Desktop Runtime.

QuickZoom er et værktøj til skærmforstørrelse. Det læser ikke tekst højt, genkender ikke tekst i billeder med OCR og ændrer ikke den faktiske tekststørrelse i dine filer. Du behøver ingen konto til almindelig brug.

### Hent og start programmet

1. Hent programmet til Windows x64 fra projektets [udgivelsesside på GitHub](https://github.com/BagerRyg/QuickZoom/releases). Vælg det færdige program, som kan startes direkte, ikke GitHubs **Source code**-arkiver med kildekode.
2. Hvis du har hentet en ZIP-fil, skal du højreklikke på den og vælge **Udpak alle** for at pakke den ud i en lokal mappe, før du starter programmet. Behold eventuelle medfølgende filer sammen.
3. Åbn **QuickZoom.exe**.
4. Gennemfør den første opsætning nedenfor. Bagefter finder du QuickZoom i meddelelsesområdet ved Windows-uret.

“Portabel” betyder, at du kan bruge QuickZoom uden at vælge den frivillige installation med automatisk start. Programmet gemmer stadig indstillinger på denne pc. Denne version kan **ikke** køre fra netværksdrev, flytbare drev eller lagerstier, der er omdirigeret eller bruger links til andre placeringer. Har du hentet programmet til en USB-enhed eller et netværksdrev, skal du først kopiere det til en almindelig lokal mappe. Arbejdspladsens begrænsninger kan betyde, at du skal have hjælp af IT.

## 3. Første opsætning trin for trin

Nye brugere starter normalt med **Stor visning** slået til, så opsætningsvinduet, teksten og knapperne er større. Slå kontakten **Stor visning** fra, hvis du vil bruge et mindre opsætningsvindue. Valget gælder selve opsætningen. Vil du senere gøre QuickZooms almindelige brugerflade større, skal du vælge **Udseende → UI-skriftstørrelse**.

Vælg **Næste** for at fortsætte og **Tilbage** for at vende tilbage til et tidligere trin. Der er syv trin:

| Trin | Hvad du vælger, og hvorfor |
| --- | --- |
| **1. Vælg dit sprog** | Vælg English, Dansk, Svenska, Norsk eller Suomi. QuickZoom starter med Windows' sprog, hvis det understøttes, og ellers med engelsk. |
| **2. Vælg tema** | **System** følger Windows. **Mørk** giver en mørk brugerflade, og **Lys** giver en lys brugerflade. Valget ændrer QuickZooms knapper og vinduer, ikke farverne i dine dokumenter. |
| **3. Vælg din aktiveringstast** | Behold **Alt** for en enkel start. Vil du ændre den, skal du klikke på tastknappen og trykke på én tast. Du holder denne tast nede, når du bruger QuickZooms genveje. Læs eventuelle advarsler om genvejskonflikter, før du fortsætter. |
| **4. Sådan bruger du QuickZoom** | Prøv at holde aktiveringstasten nede, mens du ruller eller trykker på +/−. Siden viser også, hvordan du vender farverne om og skifter zoomtilstand. Når prøvefunktionen er klar, kan du afprøve handlingerne med det samme. |
| **5. Vælg din datatilstand** | **Normal datatilstand (standard)** gemmer indstillinger lokalt og lader fejlfindingslogning være slået fra. **Streng datatilstand** blokerer desuden diagnosticerings- og nedbrudslogning, også hvis logning senere forsøges aktiveret. Ingen af tilstandene uploader dine data. |
| **6. Opsæt automatisk start** | Valgfrit. Vælg **Godkend**, hvis QuickZoom skal starte, når du logger på Windows. Vælg **Spring over**, hvis du selv vil starte programmet efter behov. |
| **7. Opsætningen er færdig** | Vælg **Afslut**. Opsætningen lukker, og QuickZoom fortsætter i meddelelsesområdet. |

**Om prøvefunktionen:** Farveinvertering er aktiveret, mens du øver dig i opsætningen. Ved almindelig brug er den som standard slået fra. Efter opsætningen skal du slå **Inverterede farver** til i menuen ved uret, før du prøver Alt+I eller Alt+midterklik.

Forstørrelsen fra øvelsen fjernes, når du forlader dette trin. Den ændrer ikke det zoomniveau, programmet starter med.

### Hvis du vælger automatisk start

Windows beder om administratortilladelse via Brugerkontokontrol, også kaldet UAC. Opsætningen forklarer, at denne usignerede version kan blive vist som **Ukendt udgiver**. Godkend kun den kopi af QuickZoom, du selv ville starte. Kan du ikke godkende som administrator, kan du vælge **Spring over** eller spørge din IT-administrator.

QuickZoom kopierer sig selv til sin installationsmappe under `%ProgramFiles%\QuickZoom` og opretter en planlagt Windows-opgave. Det gør også, at genvejene kan fungere i mange programmer, der kører med administratorrettigheder. QuickZoom giver ikke forstørrelse på Windows' beskyttede logon- eller UAC-skærme.

Vent, til opsætningen viser **Automatisk start er klar og kontrolleret.** Hvis godkendelsen afvises, eller kontrollen mislykkes, kan du vælge **Prøv igen** eller **Spring over**. Du kan opsætte automatisk start senere under **Åbn indstillinger → Om → Opsæt autostart**.

Hvis QuickZoom stadig er ved at blive klargjort, skal du vente. Vises **Prøv igen**, fordi QuickZoom endnu ikke er klar, skal du vælge den knap. Den afsluttende side vises først, når programmets ikon i meddelelsesområdet og genveje er klar.

## 4. Find og brug menuen ved uret

**Meddelelsesområdet**, også kaldet **systembakken**, er området på Windows' proceslinje ved uret. QuickZooms ikon ligner et forstørrelsesglas. Kan du ikke se det, skal du vælge **pilen opad** for at vise skjulte ikoner.

Klik på QuickZoom-ikonet med enten venstre eller højre museknap for at åbne menuen. Klikker du på ikonet igen, lukker menuen. Hvis pladsen er begrænset, mindsker menuen afstanden mellem elementerne og gør om nødvendigt tekst og ikoner mindre, så den passer på skærmen.

| Menupunkt | Hvad det gør |
| --- | --- |
| **Zoomtilstand: Fuldskærm / Linse / Fastgjort** | Vælger, hvordan forstørrelsen vises. Den valgte knap er fremhævet. Alle tilstande bruger det samme aktuelle zoomniveau. |
| **Forstørrelse** | Til gør zoomgenvejene aktive. Fra sætter zoom tilbage til 100 %. At slå funktionen til forstørrer ikke i sig selv noget. Aktive inverterede farver styres særskilt. |
| **Inverterede farver** | Aktiverer genvejene til at vende farverne om. Slå funktionen til, og brug derefter Alt+I eller Alt+midterklik. Slår du funktionen fra, fjernes inverteringen, og genvejene deaktiveres. |
| **Følg** | Åbner valgene Automatisk, Kun mus og Tastatur og indtastning. Et flueben viser den valgte tilstand. Når du vælger en tilstand, genoptages følgning også, hvis den var sat på pause. |
| **Sæt følgning på pause / Genoptag følgning** | Findes i Følg-menuen. Holder visningen stille eller lader den følge bevægelse igen. Teksten **Sat på pause** minder dig om, at følgningen er stoppet. |
| **Forstørrede skærme** | Åbner et hurtigt skærmvalg. Vælg **Hvor markøren er**, **Alle skærme** eller enkelte skærme. **Inkluderet** viser, at en skærm er med i dit valg. Valget gælder Fuldskærm. |
| **Genvejsindstillinger** | Åbner indstillingerne direkte på siden Genveje. |
| **Åbn indstillinger** | Åbner hele indstillingsvinduet på siden Generelt. |
| **Nulstil markør** | Genindlæser Windows' markørskema og anvender derefter QuickZooms markørforbedring igen, hvis den er slået til. Brug det, hvis musemarkøren ser forkert ud. Dine øvrige indstillinger nulstilles ikke. |
| **Om** | Åbner oplysninger om version, status for automatisk start, mappeknapper samt indstillinger for privatliv og logning. |
| **Afslut** | Skifter til **Er du sikker?**. Vælg igen for at lukke QuickZoom helt og fjerne programmets aktive effekter. |

Nederst vises også status for **Opstartstjeneste**. Se betydningen af de enkelte statusmeddelelser under [Om, automatisk start og privatliv](#15-om-automatisk-start-og-privatliv).

## 5. Vælg, hvordan skærmen skal forstørres

Vælg en tilstand i menuen ved uret eller under **Indstillinger → Zoom → Tilstand**.

### Fuldskærm

Det valgte skærmområde forstørres. Fordi større indhold fylder mere, kan du kun se en mindre del af skrivebordet ad gangen. QuickZoom flytter det synlige udsnit, når du bruger musen eller tastaturet. Denne bevægelse kaldes **panorering**.

Brug Fuldskærm, når du vil læse eller arbejde med forstørrelse i længere tid. Under **Skærm** kan du vælge, hvilke skærme der skal være med.

### Linse

Et flydende, forstørret område vises omkring det punkt, QuickZoom følger. Resten af skrivebordet beholder sin normale størrelse. Når QuickZoom følger musen, flyttes linsen med markøren. Ved tastaturfølgning kan den i stedet følge tekstmarkøren eller det felt eller den knap, du har valgt med tastaturet.

Brug Linse til at se nærmere på en lille knap, et ikon, en kort tekst eller en betegnelse, mens du stadig kan se resten af skrivebordet. Du kan ændre **Linsestørrelse** og **Linseform** under Zoom. Linsen er ikke et almindeligt dokumentvindue, som du kan trække eller ændre størrelse på ved at tage fat i kanten.

### Fastgjort

Et forstørret bånd eller felt vises langs en skærmkant. Det viser området omkring musemarkøren eller den tastaturposition, der bliver fulgt. Resten af skærmen beholder sin normale størrelse.

Brug Fastgjort, hvis du foretrækker at se den forstørrede tekst et fast sted. Vælg kant og størrelse under Zoom. Feltet dækker en del af arbejdsområdet. Hvis det punkt, QuickZoom følger, kommer ind under feltet, kan QuickZoom flytte feltet til den modsatte kant, så punktet fortsat er synligt.

Linse og Fastgjort bruger den skærm, hvor det fulgte punkt befinder sig. Skærmvalget til Fuldskærm begrænser dem ikke. Ved 100 % forsvinder linsen og feltet, medmindre farveinvertering er aktiv. Det gælder også, hvis **Slå forstørrelse fra ved 100 %** er slået fra.

## 6. Vælg, hvad visningen skal følge

Åbn **Følg** i menuen ved uret eller **Indstillinger → Mus → Følg**. Følgning fungerer i alle tre zoomtilstande.

| Tilstand | Hvad der sker | Velegnet, når du … |
| --- | --- | --- |
| **Automatisk (anbefalet)** | Følger indtastning og navigation med tastaturet. Når du bevidst bevæger musen, klikker eller ruller, overtager musemarkøren igen. Små, utilsigtede musebevægelser afbryder mindre let følgningen af din indtastning. | skifter mellem mus, indtastning og navigation med Tab eller piletaster. |
| **Kun mus** | Følger musemarkøren. Visningen følger ikke med, når du skifter mellem knapper eller felter med tastaturet. | selv vil styre udsnittet med musen. |
| **Tastatur og indtastning** | Følger tekstmarkøren eller det felt eller den knap, du vælger med tastaturet. Musebevægelser overtager ikke følgningen. | mest skriver eller bruger tastaturet til at bevæge dig mellem felter og knapper. |

**Tekstmarkøren** er den blinkende streg, der viser, hvor det næste bogstav bliver indsat. **Tastaturfokus** betyder den knap, det felt eller andet element, der modtager din næste handling fra tastaturet.

QuickZoom holder som regel visningen i ro, mens du læser eller skriver inden for det synlige område. Udsnittet flyttes, når tekstmarkøren nærmer sig kanten. Det kræver, at det andet program giver brugbare oplysninger om positionen. Nogle programmer, spil, fjernskriveborde og særligt udformede felter gør ikke det. Virker følgning ikke i et bestemt program, kan du flytte musen i Automatisk eller vælge Kun mus.

### Hold en tekst stille, mens du læser

1. Find den tekst, du vil læse.
2. Tryk på **Alt+F**, eller vælg **Følg → Sæt følgning på pause** i menuen ved uret.
3. Læs, uden at udsnittet følger musen eller tastaturet.
4. Tryk på Alt+F igen, vælg **Genoptag følgning**, eller vælg en tilstand under Følg for at fortsætte.

I indstillingerne betyder **Sæt følgning på pause = Til**, at følgning er sat på pause. Forstørrelsen er stadig aktiv. Den valgte følgetilstand bevares, og pauseindstillingen gemmes. Pausen stopper kun visningens følgning af bevægelse. Dit program, din indtastning og eventuel video fortsætter som normalt.

## 7. Alle genveje til daglig brug

**Hold først aktiveringstasten nede, udfør handlingen, og slip derefter tasten.** Standardgenvejene nedenfor bruger Alt. + og − på det numeriske tastatur virker også. På nogle tastaturlayout er den almindelige +‑tast delt med et andet tegn.

| Handling | Mus | Tastatur |
| --- | --- | --- |
| Zoom ind | Alt + rul opad | Alt + + |
| Zoom ud | Alt + rul nedad | Alt + − |
| Slå inverterede farver til eller fra | Alt + tryk på musehjulet/midterknappen | Alt + I |
| Skift zoomtilstand | Alt + tryk på venstre og højre museknap samtidig | Alt + Z |
| Sæt følgning på pause, eller genoptag den | Brug Følg-menuen ved uret | Alt + F |

- Genvejene til farveinvertering kræver først **Inverterede farver = Til** i menuen ved uret.
- Skift mellem tilstande sker i rækkefølgen **Fuldskærm → Fastgjort → Linse → Fuldskærm**. Z er fastlagt i denne version og kan ikke ændres særskilt i indstillingerne.
- Midterklik betyder, at du trykker musehjulet ned som en knap. Du skal ikke rulle det. Er det besværligt, kan du bruge Alt+I.
- Slip Alt, før du klikker som normalt. Mens aktiveringstasten holdes nede, kan QuickZoom opfange venstre- og højreklik som del af genvejen til at skifte tilstand.
- **Genvejstilstand** kan deaktivere enten muse- eller tastaturhandlinger. “Kun mus” kræver stadig, at du holder aktiveringstasten på tastaturet nede.
- **Forstørrelse = Fra** deaktiverer zoom, men er ikke en hovedafbryder for alle QuickZooms genveje. Genvejene til følgning og skift af tilstand kan stadig virke, og farveinvertering styres særskilt.

Hvis en QuickZoom-genvej er i konflikt med et andet program, kan du ændre dens tast under **Genveje**, hvis den kan tilpasses. For eksempel bruger mange programmer Alt+F til menuen Filer. QuickZoom kan opfange sin handling, selv om **Giv QuickZoom-genveje prioritet** er slået fra.

## 8. Brug indstillingsvinduet

Åbn menuen ved uret, og vælg **Åbn indstillinger**. Siderne står i denne rækkefølge i venstre side: **Generelt, Zoom, Skærm, Mus, Genveje, Udseende, Om**.

### Find en indstilling

- Klik på sidens navn i venstre side.
- Hvis vinduet er smalt, vises navigationen muligvis kun som ikoner. Brug menuknappen med tre vandrette streger til at **vise eller skjule navigationsnavne**.
- Klik i **Søg i indstillinger**, eller tryk på **Ctrl+F**, og skriv for eksempel `markør`, `sprog` eller `zoom`.
- Klik på et søgeresultat, eller brug pil op/ned og Enter. QuickZoom åbner den relevante side og viser indstillingen, når det er muligt.
- Indstillingerne til Linse og Fastgjort vises kun, når den pågældende tilstand er valgt. En søgning efter “linsestørrelse” skifter ikke automatisk zoomtilstand.

Du skal skrive direkte i søgefeltet og talfelterne. I denne version understøtter de ikke kopiering, klipning, indsætning eller træk og slip.

### Skift en indstilling

| Betjening | Sådan bruger du den |
| --- | --- |
| **Til/Fra-kontakt** | Klik for at skifte. Til betyder aktiveret. Bemærk, at **Sæt følgning på pause** netop aktiverer en pause. |
| **Rulleliste** | Åbn listen, og vælg ét punkt. |
| **Skyder og tal** | Træk skyderen, eller klik på tallet og skriv et præcist heltal inden for det tilladte interval. Tryk på Enter, eller forlad feltet for at anvende tallet. Escape annullerer en ufærdig ændring af et tal. |
| **Farvepalette** | Klik på et farvefelt. Den valgte farve markeres og vises i forhåndsvisningen af markøren. |
| **Tastknap til en genvej** | Klik, og tryk derefter på den enkelte tast, du vil bruge. Læs bemærkningen om at vælge taster under Genvejsindstillinger. |

Brug **Tab/Shift+Tab** til at bevæge dig frem og tilbage mellem felter og knapper. Mellemrum eller Enter aktiverer knapper og kontakter. Piletasterne betjener lister og skydere. Rul ned, hvis hele siden ikke kan være i vinduet.

Ændringer træder normalt i kraft med det samme og gemmes automatisk. Der er ingen særskilt Anvend-knap og ingen knap til at fortryde alle ændringer. **Færdig** eller vinduets **X** lukker indstillingerne, mens QuickZoom fortsætter. Escape rydder først en aktiv søgning eller annullerer visse igangværende redigeringer; ellers lukker den indstillingerne. Den fortryder ikke ændringer, der allerede er anvendt.

## 9. Generelle indstillinger

| Indstilling | Standard | Hvad den betyder |
| --- | --- | --- |
| **Jævn zoom** | Til | Animerer overgangen mellem zoomniveauer i stedet for at springe direkte. Slå den fra, hvis du foretrækker øjeblikkelige skift eller synes, at animationen er ubehagelig. |
| **Slå forstørrelse fra ved 100 %** | Til | Stopper den aktive forstørrelse, når du vender tilbage til normal størrelse, medmindre en aktiv farveinvertering stadig har brug for den. QuickZoom afsluttes ikke, og du kan stadig zoome ind igen. |
| **Centrér markør** | Fra | I Fuldskærm placeres det punkt, der følges, tættere på midten af den forstørrede visning. Skærmbilledet kan derfor bevæge sig mere, når du flytter musen. Skærmens kanter begrænser, hvor langt udsnittet kan centreres. Selve musemarkøren flyttes ikke. |

## 10. Zoomindstillinger

### Tilstand og dens ekstra valg

**Tilstand** vælger Fuldskærm, Linse eller Fastgjort. Fuldskærm er standardvalget. Kun de indstillinger, der passer til den valgte tilstand, bliver vist.

| Indstilling | Standard og interval | Hvad den betyder |
| --- | --- | --- |
| **Linsestørrelse** | 360 px; 100–1400 px | Indstiller linsens bredde. En større linse viser mere ad gangen, men dækker mere af det almindelige skrivebord. “px” betyder skærmpixels. |
| **Linseform** | Rektangel; også Firkant eller Rund | Rektangel er bredt med forholdet 16:9 mellem bredde og højde. Firkant og Rund er lige brede og høje. Formen ændrer linsens omrids, ikke zoomniveauet. |
| **Placering af fastgjort felt** | Øverst; også Nederst, Venstre, Højre | Vælger, hvilken kant feltet som udgangspunkt skal ligge ved. Det kan flyttes til den modsatte kant, hvis det ellers ville dække det punkt, der følges. |
| **Feltstørrelse** | 25 %; 10–50 % | Angiver feltets højde ved Øverst/Nederst eller dets bredde ved Venstre/Højre som en andel af skærmen. Valget bestemmer, hvor meget plads feltet optager, ikke hvor kraftig forstørrelsen er. |

Linsen og feltet tilpasses den tilgængelige skærmplads. Din skærm kan derfor begrænse deres faktiske størrelse. Der er ingen særskilt indstilling af linsehøjde eller et separat zoomniveau for hver tilstand.

### Niveau og hastighed

| Indstilling | Standard og valgmuligheder | Hvad den betyder |
| --- | --- | --- |
| **Zoomtrin (%)** | 30; 1–200 | Hvor mange procentpoint ét trin på musehjulet eller ét tryk på en zoomtast tilføjer eller fjerner. Prøv 10, hvis du vil justere mere præcist. Højere værdier giver hurtigere kraftig forstørrelse. |
| **Maks. zoom (%)** | 400; 150–750 | Det højeste tilladte zoomniveau. 400 % er fire gange normal størrelse. Dette er en grænse, ikke dit aktuelle zoomniveau. Sænker du grænsen, kan en igangværende større forstørrelse blive reduceret. Det laveste zoomniveau er altid 100 %. |
| **Opdateringshastighed** | 120 Hz; 60, 90, 120, 180, 240 Hz eller Ubegrænset | Hvor ofte QuickZoom forsøger at opdatere bevægelser og animationer. Højere værdier kan give mere jævn bevægelse, men kræver mere af computeren. Prøv 60 Hz, hvis pc'en virker belastet, eller bevægelserne hakker. |

**Ubegrænset** bruger den højeste opdateringshastighed, der registreres blandt de tilsluttede skærme, dog med et mål på mindst 60 Hz. Det betyder ikke uendeligt mange opdateringer og garanterer ikke, at pc'en kan nå hastigheden. Hz betyder opdateringer pr. sekund.

## 11. Skærmindstillinger

Disse indstillinger styrer forstørrelse i **Fuldskærm**. De er især nyttige, hvis din pc har mere end én skærm.

| Indstilling | Standard | Hvad den betyder |
| --- | --- | --- |
| **Skift automatisk skærm** | Til | Lader den aktive fuldskærmsvisning flytte mellem skærme, når det fulgte punkt flytter sig. Fra låser den aktive skærm. Det har især betydning sammen med **Hvor markøren er**. Det fjerner ikke skærme fra valget Alle skærme. |
| **Forstørrede skærme: Alle skærme** | Valgt | Medtager alle tilsluttede skærme i fuldskærmsforstørrelsen. |
| **Forstørrede skærme: Hvor markøren er** | — | Bruger én aktiv skærm i stedet for alle. Normalt er det skærmen med musemarkøren. Ved tastaturfølgning kan tekstmarkørens eller tastaturfokussets position i stedet afgøre det. Behold Skift automatisk skærm slået til, hvis visningen skal kunne flytte mellem skærme. |
| **Forstørrede skærme: Brugerdefineret valg** | — | Viser en kontakt for hver skærm, så du selv kan vælge, hvilke der er med. Mindst én skal være valgt. |
| **Identificer skærme** | Handlingsknap | Viser kortvarigt en betegnelse på hver skærm, for eksempel Primær, Sekundær eller Skærm 3, som passer til listen. Betegnelserne forsvinder efter cirka tre sekunder. Escape lukker dem tidligere. |

Brug **Identificer skærme** til at se, hvilke fysiske skærme QuickZooms navne henviser til. **Primær** og **Sekundær** svarer ikke nødvendigvis til, hvilken skærm Windows i øjeblikket bruger som hovedskærm. QuickZooms liste vælger kun, hvad der skal forstørres. Den ændrer ikke Windows' hovedskærm, opløsning eller skærmplacering.

Du kan også hurtigt ændre valget via **Forstørrede skærme** i menuen ved uret. Vælger du enkelte skærme, skifter QuickZoom til et brugerdefineret valg. Kontrollér valget igen, når du tilslutter, frakobler eller omarrangerer skærme. QuickZoom opdaterer skærmlisten og vælger et gyldigt valg, hvis det gamle ikke længere kan bruges.

## 12. Museindstillinger

Denne side indeholder både følgning af tastaturet og indstillinger for musemarkørens udseende.

| Indstilling | Standard | Hvad den betyder |
| --- | --- | --- |
| **Følg** | Automatisk (anbefalet) | Vælger Automatisk, Kun mus eller Tastatur og indtastning. Se [afsnittet om følgning](#6-vælg-hvad-visningen-skal-følge). Valg af en tilstand genoptager følgningen. |
| **Sæt følgning på pause** | Fra | Til holder visningen stille. Slå den fra for at genoptage den valgte følgetilstand. |
| **Find markør ved rystelse** | Til | Bevæg hurtigt musen frem og tilbage for at vise en kortvarig fremhævning omkring markøren. Du behøver ingen genvejstast. Det ændrer ikke zoomniveauet. |
| **Markørforbedring** | Fra | Bruger QuickZooms større eller farvede udgaver af de almindelige Windows-markører, mens programmet kører. Slå den fra for at vende tilbage til Windows' normale markørskema. Nogle programmer tegner deres egen markør og kan se anderledes ud. |
| **Forhåndsvisning** | Kun visning | Viser den valgte markørstørrelse, fyldfarve og kant. Du kan prøve dig frem, mens Markørforbedring er slået fra. |
| **Markørstørrelse** | 100 %; 100–500 % | Skalerer QuickZooms forbedrede markør. 200 % er dobbelt så stor som dens grundstørrelse. Skærmforstørrelsen ændres ikke. |
| **Markørfarve** | Hvid | Farven inde i den forbedrede markør. Vælg blandt farvefelterne. |
| **Kantfarve** | Sort | Markørens omrids. En tydeligt anderledes kantfarve gør det lettere at se markøren på både lyse og mørke baggrunde. |

Paletterne har 48 forudvalgte farver. Du kan ikke indtaste din egen farvekode. Du får en advarsel, hvis fyld og kant ligner hinanden for meget, men du kan stadig beholde farverne. Prøv et klart farvet fyld med sort kant, hvis markøren er svær at finde.

Der kan gå et øjeblik, før ændringer i størrelse og farve ses på selve markøren. Ændringerne påvirker den kun, når **Markørforbedring** er slået til. **Nulstil markør** i menuen opfrisker markøren, men anvender forbedringen igen, hvis den stadig er aktiveret. Afslutning af QuickZoom gendanner Windows' normale markørskema.

## 13. Genvejsindstillinger

| Indstilling | Standard | Hvad den betyder |
| --- | --- | --- |
| **Genvejstilstand** | Begge | **Begge** aktiverer muse- og tastaturhandlinger. **Kun tastatur** deaktiverer QuickZooms genveje med musehjul, midterklik og begge museknapper. **Kun mus** deaktiverer programmets tastaturhandlinger, men kræver stadig tastaturets aktiveringstast til musehandlingerne. |
| **Aktiveringstast** | Alt | Den ene tast, du holder nede, før du udfører en QuickZoom-handling. Ændrer du den, ændres den første tast i alle kombinationerne i denne vejledning. |
| **Tast til inverterede farver** | I | Den anden tast i tastaturgenvejen til farveinvertering: som standard Alt+I. Midterklik er stadig musealternativet. Inverterede farver skal være slået til i menuen ved uret. |
| **Tast til pause/genoptag følgning** | F | Den anden tast i genvejen til at sætte følgning på pause eller genoptage den: som standard Alt+F. Virker i alle tre zoomtilstande. |
| **Giv QuickZoom-genveje prioritet** | Fra | Hjælper med at forhindre, at taster, du bruger til QuickZoom, samtidig udløser handlinger eller menuer i et andet program. Nyttigt, hvis zoom åbner en programmenu. Dette er en generel genvejsindstilling, ikke kun til Office. |

QuickZoom kan stadig opfange sine kendte handlingstaster, når prioritet er slået fra. Kontakten løser ikke alle genvejskonflikter. Vælg andre taster, eller skift Genvejstilstand, hvis et andet program har brug for samme kombination. Indtastning med AltGr og øvrige Windows-genveje skal fortsat være tilgængelige.

### Skift en tast sikkert

1. Klik på knappen med den aktuelle tast ud for indstillingen.
2. Tryk på **én tast**, ikke hele kombinationen. Vælg for eksempel F som den anden tast; forsøg ikke at indtaste Alt+F i dialogen.
3. Dialogen lukker, og tasten anvendes med det samme.
4. Se efter en advarsel eller rød konfliktmeddelelse, og afprøv derefter handlingen.

**Vil du annullere valg af tast, skal du klikke på dialogens X, før du trykker på en tast. Tryk ikke på Escape: i denne version kan Escape blive valgt som selve genvejstasten.**

Brug forskellige taster til de tre genvejsindstillinger. Undgå Z, fordi den også bruges som den faste genvej til skift af zoomtilstand. Undgå +/− som aktiveringstast, fordi de bruges til zoom. En advarsel eller konfliktmeddelelse i indstillingerne forhindrer ikke nødvendigvis, at et problematisk valg bliver gemt. Vælg en anden tast, hvis der vises en advarsel, eller en handling holder op med at virke.

Alt er et praktisk udgangspunkt. Almindelige bogstavtaster kan forstyrre indtastning. Caps Lock ændrer brugen af store bogstaver. Windows-taster kan være i konflikt med Windows, og Fn afhænger af tastaturet. AltGr kan ikke bruges som erstatning for Alt. Du kan gendanne standardtasterne hver for sig ved at vælge Alt, I og F.

## 14. Indstillinger for udseende

| Indstilling | Valgmuligheder | Hvad den betyder |
| --- | --- | --- |
| **Tema** | Følg Windows (standard), Mørk, Lys | Ændrer farverne i QuickZooms egen brugerflade. Følg Windows tilpasser sig Windows-temaet. Valget vender ikke skærmens farver om. |
| **Sprog** | English, Dansk, Svenska, Norsk, Suomi | Ændrer sproget i QuickZooms menuer, opsætning, indstillinger og meddelelser. Det ændrer ikke sproget i Windows eller i dine dokumenter. |
| **UI-skriftstørrelse** | Følg Windows (standard), Stor, Ekstra stor | Gør teksten i QuickZooms egen brugerflade lettere at læse. Stor og Ekstra stor øger størrelsen ud over Windows' indstilling for tekststørrelse. Dette er uafhængigt af skærmzoom og markørstørrelse. |

Ændringer anvendes straks og kan få indstillingsvinduet til at blive bygget op igen. Hvis der ikke er plads til alle indstillinger, kan du gøre vinduet større eller rulle. QuickZoom tager også hensyn til Windows' tilgængelighedsvalg, såsom farver med høj kontrast og færre animationer i brugerfladen. Det er Windows-indstillinger, ikke ekstra kontakter i QuickZoom.

## 15. Om, automatisk start og privatliv

### Build og opstart

**Build og opstart** viser QuickZooms version/build og status for automatisk start. “Opstartstjeneste” i statusfeltet henviser til QuickZooms planlagte opstartsopgave.

| Status | Betydning og næste skridt |
| --- | --- |
| **Konfigureret** | Automatisk start er klar. QuickZoom bør starte, når du logger på Windows. |
| **Ikke konfigureret** | Start selv QuickZoom, eller vælg **Opsæt autostart**. |
| **Skal repareres** | Den gemte opstartskonfiguration kræver opmærksomhed. Vælg **Opsæt autostart**, og gennemfør godkendelsen og kontrollen. |
| **Status utilgængelig** | QuickZoom kunne ikke fastslå status. Kontrollér igen senere. Brug om nødvendigt Opsæt autostart, eller bed IT om hjælp. |

Knappen **Opsæt autostart** vises, når automatisk start ikke er klar. Den åbner den del af opsætningen, der handler om opstart. Der er ingen kontakt i programmet til at slå en allerede oprettet opstartsopgave fra. Se [afsnittet om fjernelse og automatisk start](#18-gem-sikkerhedskopiér-nulstil-opdatér-eller-fjern-quickzoom).

### Placeringer

- **Åbn appmappe** åbner programmets installationsmappe i Stifinder. Knappen er ikke tilgængelig, hvis QuickZoom ikke er installeret via opsætningen af automatisk start. Det er normalt, når du kører en hentet kopi direkte.
- **Åbn indstillingsmappe** åbner mappen med dine indstillinger, normalt `%LOCALAPPDATA%\QuickZoom`. Stifinder kan vise mappen med indstillingsfilen markeret.

### Privatliv og diagnosticering

| Indstilling eller knap | Standard | Hvad den gør |
| --- | --- | --- |
| **Streng datatilstand** | Fra, medmindre valgt under opsætningen | Blokerer QuickZooms diagnosticerings- og nedbrudslogning. Slår du den til, stoppes aktiv fejlfindingslogning, og logningskontakten deaktiveres. Indstillinger gemmes stadig, og eksisterende logfiler bevares. |
| **Fejlfindingslogning** | Fra | Registrerer begrænsede tekniske hændelser **kun indtil QuickZoom afsluttes**. Logning slås fra igen, når programmet genstartes. Streng datatilstand skal være slået fra, før du kan aktivere logning. |
| **Lokale logfiler → Vis logfil** | Tilgængelig, når en logfil findes | Åbner Stifinder med den lokale logfil markeret. Åbn filen derfra, hvis du vil læse den. |

Logfiler indeholder tekniske hændelsesoplysninger, såsom tidspunkter, buildnumre, identifikation af hændelser og deres kilder, fejltyper og fejlkoder. De registrerer ikke indtastede taster, dokument- eller skærmindhold, vinduestitler eller rå fejlmeddelelsestekster. Følgning bruger positionsoplysninger fra Windows og programmerne; den behøver ikke læse den tekst, du skriver.

QuickZoom gemmer højst to logfiler på op til 1 MB hver og erstatter ældre poster efter behov. Filerne bliver på din pc og uploades ikke. Der oprettes ingen diagnosticerings- eller nedbrudslogfiler fra programmet, medmindre du aktiverer fejlfindingslogning. Streng datatilstand sletter ikke gamle logfiler og forhindrer ikke normal lokal lagring af indstillinger.

Vil du indsamle oplysninger om et problem, skal du slå logning til, gentage det, der udløser problemet, vælge Vis logfil og derefter slå logning fra. Gennemse selv filen, før du eventuelt vælger at dele den. Hvis logning ikke kan skrive til disken, slås den fra, og du får en meddelelse. Kontrollér ledig plads og tilladelser.

Når logning er slået til, kan Om også vise **Temamotor**. Oplysningen fortæller support, hvilken metode programmet bruger til at tegne brugerfladen. Det er information, ikke en indstilling, du skal ændre.

## 16. Eksempler fra hverdagen

### Læs en lang e-mail, webside eller et dokument

Vælg **Fuldskærm** og **Automatisk** følgning. Zoom et par trin ind. Brug musen, piletasterne eller almindelig rulning til at læse. Vil du studere en passage uden bevægelse, kan du trykke på Alt+F for at sætte følgning på pause. Tryk igen, før du går videre. Prøv et mindre Zoomtrin, hvis ét trin ændrer størrelsen for meget.

### Undersøg en lille knap uden at miste overblikket

Vælg **Linse** og derefter **Kun mus**, hvis du vil styre udsnittet helt med markøren. Zoom ind, og flyt markøren hen over knappen. En rektangulær linse er god til brede tekster; Rund eller Firkant kan passe til ikoner. Zoom ud igen, når du er færdig.

### Skriv med forstørret visning

Brug **Automatisk** til almindelig skrivning. Vælg **Tastatur og indtastning**, hvis musebevægelser ikke skal flytte visningen. Klik i et tekstfelt, og begynd at skrive. QuickZoom følger tekstmarkøren, hvis programmet gør dens position tilgængelig. Prøv **Fastgjort**, hvis du foretrækker at læse den forstørrede tekst i et fast bånd. Virker følgningen ikke i programmet, kan du bruge Kun mus og placere udsnittet selv.

### Forstør én skærm, og behold en anden til overblik

Vælg **Fuldskærm**, gå til **Indstillinger → Skærm → Forstørrede skærme → Brugerdefineret valg**, og medtag den skærm, du vil forstørre. Brug **Identificer skærme**, hvis du er i tvivl om, hvilken der er hvilken. Du kan i stedet vælge **Hvor markøren er** med Skift automatisk skærm slået til, så den forstørrede visning flytter mellem skærmene.

### Find musemarkøren på en travl skærm

Behold **Find markør ved rystelse** slået til, og bevæg hurtigt musen frem og tilbage. Vil du altid have en tydeligere markør, kan du aktivere **Markørforbedring**, øge Markørstørrelse og vælge tydeligt forskellige fyld- og kantfarver. Begynd med en moderat størrelse, så markøren ikke dækker små knapper.

### Dæmp en skarp, lys side

Slå **Inverterede farver** til i menuen ved uret, og tryk derefter på **Alt+I**. Brug Alt+I igen for at gendanne de oprindelige farver, mens genvejen forbliver aktiveret. Vil du deaktivere funktionen helt, skal du slå kontakten fra i menuen. Invertering vender farverne om i den forstørrede visning, også i billeder. Den ændrer ikke dokumentet og slår ikke programmets eget mørke tema til.

### Brug en langsommere pc, eller få mindre bevægelse

Prøv **Opdateringshastighed: 60 Hz**, et lavere zoomniveau og **Jævn zoom: Fra**. Sæt følgning på pause, når du læser et fast område. Det er forslag, du kan afprøve, ikke nødvendige indstillinger.

### Vis detaljer under en demonstration

Linse kan fremhæve en lille detalje i et program, mens resten af skrivebordet stadig er synligt. En større, forbedret markør kan også hjælpe. Hvis du deler eller optager skærmen, skal du først kontrollere, hvad publikum faktisk ser. Forskellige programmer til skærmdeling kan optage forstørrelsesfelter og effekter forskelligt. Video og beskyttet indhold kan også opføre sig forskelligt afhængigt af program og skærmdriver.

## 17. Fejlfinding

| Problem | Hvad du kan prøve |
| --- | --- |
| **QuickZoom ser ud til at forsvinde efter opsætningen.** | Se ved Windows-uret og under pilen til skjulte ikoner. QuickZoom kører normalt i meddelelsesområdet uden et permanent hovedvindue. |
| **Forstørrelse er slået til, men skærmen har normal størrelse.** | Hold aktiveringstasten nede, og zoom ind. Til aktiverer genvejene, men vælger ikke et zoomniveau over 100 %. |
| **Zoomgenvejene gør ingenting.** | Kontrollér, at QuickZoom kører, Forstørrelse er slået til, og Genvejstilstand tillader den valgte betjening. Kontrollér Aktiveringstast. Med standardvalget skal du bruge venstre Alt og ikke AltGr. |
| **Jeg kan ikke zoome mere ind.** | Kontrollér Maks. zoom under Zoom. Du kan allerede have nået grænsen. |
| **Visningen er for stor, eller jeg har mistet overblikket.** | Hold aktiveringstasten nede, og zoom ud gentagne gange. Du kan også slå Forstørrelse fra i menuen ved uret. Slå om nødvendigt også Inverterede farver fra. Afslut lukker programmet helt. |
| **Visningen står stille.** | Se efter Sat på pause ved Følg. Genoptag følgning, eller vælg en følgetilstand. I Tastatur og indtastning overtager musebevægelser bevidst ikke følgningen. |
| **Det, jeg skriver, forsvinder uden for det forstørrede område.** | Vælg Automatisk eller Tastatur og indtastning, og genoptag følgning. Hvis kun ét program er berørt, stiller det muligvis ikke brugbare tilgængelighedsoplysninger til rådighed. Brug Kun mus til at placere visningen i stedet. |
| **Linsen eller det fastgjorte felt er væk.** | Zoom over 100 %. Kontrollér, at Forstørrelse er slået til. Linsen og feltet forsvinder normalt ved almindelig størrelse. |
| **Det fastgjorte felt skifter kant.** | Det er forventet, når det ellers ville dække det punkt, der følges. Feltet bruger den modsatte kant for at holde punktet synligt. |
| **Den forkerte skærm forstørres.** | Åbn Skærm, identificér skærmene, og gennemgå Alle skærme/Hvor markøren er/Brugerdefineret valg. Slå Skift automatisk skærm til, hvis den aktive visning skal kunne flytte. Disse valg gælder Fuldskærm. |
| **Inverterede farver virker ikke.** | Slå først Inverterede farver til i menuen ved uret, og brug derefter genvejen. Kontrollér Genvejstilstand og den valgte tast til invertering. Prøvefunktionen under opsætningen aktiverer invertering særskilt. |
| **Farverne blev ikke normale, da jeg slog Forstørrelse fra.** | Slå også Inverterede farver fra. Zoom og farveinvertering styres hver for sig. |
| **Alt åbner et andet programs menu, eller en genvej er i konflikt.** | Prøv Giv QuickZoom-genveje prioritet, eller skift den tast, der giver problemer. Ved konflikt mellem Alt+F og en Filer-menu kan du ændre tasten til pause/genoptag eller vælge Kun mus under Genvejstilstand, hvis det passer til din brug. |
| **Museklik opfører sig mærkeligt, mens jeg zoomer.** | Slip aktiveringstasten, før du klikker som normalt. Når den holdes nede, aktiveres genvejen med venstre og højre museknap til at skifte tilstand. |
| **En genvej holdt op med at virke, efter at jeg ændrede tasten.** | Gennemgå tastknapper og advarsler. Vælg forskellige taster; gendan Alt/I/F, hvis du er i tvivl. Hvis Escape blev valgt ved en fejl, skal du vælge den ønskede tast igen. |
| **Zoom virker i almindelige programmer, men ikke i et program med administratorrettigheder.** | Opsæt automatisk start under Om, og godkend Windows-meddelelsen. Gem dit arbejde, log af Windows, og log på igen, så den oprettede opgave starter QuickZoom. Spørg IT, hvis du ikke kan godkende opsætningen. |
| **Markørens størrelse eller farve ændres ikke.** | Slå Markørforbedring til. Forhåndsvisningen virker også, når forbedringen er slået fra. Vent et øjeblik på ændringen. Programmer med deres egen musemarkør bruger muligvis ikke den forbedrede Windows-markør. |
| **Musemarkøren ser forkert ud.** | Prøv Nulstil markør i menuen ved uret. Slå Markørforbedring fra, hvis du vil fjerne QuickZooms ændringer. Afslut om nødvendigt QuickZoom, og vælg dit markørskema igen i Windows. |
| **Teksten i indstillingerne er for lille.** | Vælg Udseende → UI-skriftstørrelse → Stor eller Ekstra stor. Stor visning under opsætningen ændrer ikke den daglige brugerflade. |
| **Jeg kan ikke indsætte tekst i søgefeltet eller et talfelt.** | Skriv værdien direkte. Udklipsholderhandlinger er deaktiveret i disse felter. |
| **Et indtastet tal afvises.** | Skriv et heltal inden for det viste interval. Skriv ikke enheden % eller px. |
| **Indstillingerne kunne ikke gemmes.** | Tidligere gemte indstillinger bevares. Kontrollér ledig diskplads og skrivetilladelse til den lokale indstillingsmappe. Ændr derefter indstillingen igen for at prøve på ny. Regn ikke med, at det viste valg bevares ved genstart. |
| **Indstillingerne kunne ikke indlæses.** | QuickZoom lader den gemte fil være uændret, bruger midlertidige standardindstillinger og blokerer yderligere automatisk lagring. Sikkerhedskopiér den oprindelige fil. Gendan derefter en sikkerhedskopi, du ved virker, eller vælg bevidst Gendan standardindstillinger for at erstatte filen med standardværdier. Se anvisningerne nedenfor. |
| **Opsætning eller godkendelse af automatisk start mislykkes.** | Kontrollér tilladelser, og vælg Prøv igen, eller vælg Spring over og start selv programmet. Brug senere Om → Opsæt autostart. Hvis UAC-godkendelsen afvises, bliver den ønskede opsætning eller opdatering ikke godkendt. En ældre installation med automatisk start kan stadig findes. |
| **QuickZoom siger, at programmet allerede kører.** | Brug dialogens Åbn indstillinger, eller find det eksisterende ikon ved uret. Fortsæt ikke med at starte flere kopier. |
| **QuickZoom afviser sin placering.** | Flyt det hentede program til en almindelig mappe på et lokalt, fast drev. Netværksdrev, flytbare drev og omdirigerede lagerstier afvises. Spørg IT, hvis din mappe til brugerdata er omdirigeret. |
| **Forstørrelsen svigter, viser sorte områder eller opfører sig forkert efter dvale eller skærmændringer.** | Vend tilbage til 100 %, afslut QuickZoom, og åbn det igen. Kontrollér tilstand og skærmvalg. Undgå at køre et andet skærmforstørrelsesprogram samtidig under fejlfinding. Slå om nødvendigt logning til for den aktuelle session, og gentag problemet. QuickZoom kan slå forstørrelse fra efter en opstartsfejl for at undgå sorte felter på skærmen. |

Når du beder om support, er det nyttigt at oplyse version/build, Windows-version, antal skærme, zoomtilstand, følgetilstand og de præcise trin, der udløser problemet. Medtag kun dokumentindhold eller andre personlige oplysninger, hvis du selv vælger at dele dem.

## 18. Gem, sikkerhedskopiér, nulstil, opdatér eller fjern QuickZoom

### Hvad der bliver gemt

Indstillinger gemmes automatisk for din Windows-bruger, normalt her:

```text
%LOCALAPPDATA%\QuickZoom\settings.json
```

Åbn **Om → Åbn indstillingsmappe** for at finde filen uden at skrive stien. Samme mappe indeholder opsætningsstatus i `first-run-setup.json` og eventuelle lokale fejlfindingslogfiler. Opsætningsstatus husker, om opsætningen er gennemført, og om automatisk start blev sprunget over.

De gemte indstillinger omfatter dine genvejstaster, tilstande, skærmvalg, markørudseende, valg for følgning og pause samt privatlivsvalg. Den aktuelle zoomprocent gemmes ikke som startniveau: en ny start begynder ved 100 %. Fejlfindingslogning starter også slået fra hver gang. Var farverne inverteret, da du afsluttede, gendanner QuickZoom effekten næste gang, også ved 100 %.

### Sikkerhedskopiér og gendan indstillinger

1. Åbn **Om → Åbn indstillingsmappe**, og lad Stifinder-vinduet stå åbent.
2. Afslut QuickZoom ved at vælge Afslut og derefter bekræfte i menuen ved uret.
3. Find `settings.json` i den mappe, du åbnede, og kopiér filen til en mappe til sikkerhedskopier. Vil du også gemme den første opsætnings valg og status, skal du kopiere `first-run-setup.json`.
4. Når du vil gendanne indstillingerne, skal QuickZoom være lukket. Kopiér de sikkerhedskopierede filer tilbage til samme indstillingsmappe. Erstat kun de nuværende filer, hvis det er din hensigt.
5. Start QuickZoom, og kontrollér indstillingerne. Kontrollér skærmvalget igen, hvis du bruger en anden pc eller har ændret skærmene.

En sikkerhedskopi af indstillingerne installerer ikke programmet og genskaber ikke dets Windows-opstartsopgave. Automatisk start skal opsættes særskilt. Der er ingen import- eller eksportknap i programmet.

### Nulstil alle indstillinger

Brug dette, når du vil begynde forfra med QuickZooms indstillinger og ikke blot zoome ud.

1. Åbn indstillingerne, og klik på **Gendan standardindstillinger** nederst i vinduet.
2. Knappen skifter til **Nulstil alt?**.
3. Klik igen inden for ti sekunder for at bekræfte. Ellers udløber bekræftelsen, uden at noget nulstilles.

Nulstilling sætter zoom til 100 %, fjerner aktiv farveinvertering, slår markørforbedring fra og gendanner standardindstillingerne, herunder genveje og sprogvalg. Streng datatilstand slås fra igen. Nulstilling fjerner ikke det installerede program, sletter ikke eksisterende logfiler og deaktiverer ikke automatisk start. Knappen starter heller ikke guiden til første opsætning forfra.

### Gennemgå opsætningen igen

Vil du åbne opsætningsguiden igen, skal du først afslutte QuickZoom. Start derefter programmet med tilvalget `-setup`. Det kan du for eksempel gøre ved at tilføje et mellemrum og `-setup` i feltet **Destination** i egenskaberne for en Windows-genvej til QuickZoom. Tilføj det efter eventuelle anførselstegn:

```text
"C:\DinQuickZoomMappe\QuickZoom.exe" -setup
```

Erstat eksempelstien med QuickZooms faktiske placering. Guiden indlæser dine nuværende valg, så du kan gennemgå dem; den nulstiller dem ikke. Fjern `-setup` fra genvejen bagefter, hvis den igen skal starte programmet som normalt.

### Opdatér QuickZoom

Programmet har ingen knap til at søge efter opdateringer. Hent den nyere udgave fra projektets udgivelsesside, afslut den kørende QuickZoom, og pak alle medfølgende filer ud sammen i en almindelig lokal mappe. Kør den nye QuickZoom.exe. Dine eksisterende indstillinger genbruges normalt. Hvis du bruger automatisk start, skal du tillade en eventuel opdatering af installation eller opstart og bagefter kontrollere det nye buildnummer og status under **Om**. Følg anvisningerne til reparation af opstart, hvis det er nødvendigt.

Lad opsætningen opdatere installationsmappen. Udskift ikke selv enkelte filer i dens `versions`-mapper.

Behold sikkerhedskopien af dine indstillinger, indtil du har kontrolleret den nye version. En installeret kopi og en ældre hentet kopi kan ligge på disken samtidig. Kontrollér derfor versionen i det program, der faktisk kører, frem for at stole på mappens navn.

### Stop automatisk start

Når du afslutter QuickZoom, stoppes det her og nu. Det forhindrer ikke programmet i at starte, næste gang du logger på. Nulstilling af indstillinger deaktiverer heller ikke automatisk start.

Denne version har ingen kontakt i programmet til at slå automatisk start fra. Sådan deaktiverer du den uden at slette dine indstillinger:

1. Åbn **Opgavestyring** fra Windows' Start-menu. Administratortilladelse kan være nødvendig.
2. Åbn **Opgavestyringsbibliotek**, og find **QuickZoom Startup (Elevated)**.
3. Deaktiver denne opgave. Du kan aktivere den igen senere eller bruge QuickZooms Opsæt autostart, hvis opstarten skal repareres.

På en arbejdscomputer kan du bede IT om at ændre det, hvis du ikke selv kan redigere opgaven. Deaktiver ikke andre Windows-opgaver.

Hvis QuickZoom senere tilbyder at opsætte automatisk start igen, skal du vælge **Spring over** for fortsat at starte programmet manuelt.

### Fjern QuickZoom helt

1. Afslut QuickZoom.
2. Hvis du har opsat automatisk start, skal du fjerne opgaven **QuickZoom Startup (Elevated)** i Opgavestyring, så den ikke længere forsøger at starte programmet.
3. Slet den QuickZoom-programmappe, du hentede. Ved installation via automatisk start skal du slette `%ProgramFiles%\QuickZoom`. Det kræver normalt administratortilladelse.
4. Vil du også slette indstillinger, opsætningsstatus og gamle logfiler, skal du fjerne `%LOCALAPPDATA%\QuickZoom`, efter at du har gemt de sikkerhedskopier, du vil beholde.

Der er ingen afinstallationskommando i programmet. Kildekodepakken indeholder scripts til administratorer, der vil fjerne opstartsopgaven; de er ikke nødvendige til almindelig brug. Sletning af programfiler alene sletter ikke indstillingerne, og sletning af indstillingerne alene fjerner ikke opstartsopgaven.
