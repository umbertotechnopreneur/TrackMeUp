# Dati del planner

Sei CSV **UTF-8 con BOM**, separatore `;`, decimali con punto, date ISO. Non rinominare le intestazioni. I campi che contengono `;` vanno tra virgolette doppie.

**Parametri B25** indica la cartella. **Dati → Aggiorna tutto** ricarica le tabelle; non controlla continuamente i file. Le ultime copie rimangono nel workbook offline e anche dopo un aggiornamento fallito: controllare **Query e connessioni** e i timestamp.

| File | Schema | Contenuto |
| --- | --- | --- |
| `Citta.csv` | `Code;City;WindowsId;IanaId;Latitude;Longitude;CountryCode` | 206 città; codice univoco, coordinate e ISO paese |
| `Periodi_DST.csv` | `Code;FromUtc;UntilUtc;OffsetHours` | 670 intervalli UTC, 2026–2028 |
| `Calendari.csv` | `Calendar;Date;Name;Active;Source;Kind;Status;Version;Updated` | 542 festività/recuperi, 2026–2028 |
| `Santi.csv` | `Month;Day;Name;Source` | 211 voci aggregate in 191 date |
| `Fasi_lunari.csv` | `Utc;Phase;Between;Source` | 247 eventi lunari USNO, 2025–2029 |
| `Meteo.csv` | `Code;ObservedUtc;FetchedUtc;TemperatureC;FeelsLikeC;Description;WindMs;Status;Source` | Ultime osservazioni OpenWeather, inizialmente sette città |

## Città e DST

Fonte canonica: `WorkTrail/Assets/WorldClocks/world-clocks.sqlite3`, letto senza modificarlo. Il codice corrisponde esattamente a `city.id`; nome della colonna e città selezionata sono indipendenti. Scrivere un nome diverso non cambia il fuso.

Dati città: [GeoNames](https://www.geonames.org/), [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/), come registrato nel DB. L'esportazione conserva i quattro campi precedenti e aggiunge latitudine, longitudine e paese. SHA-256 del DB: `43549bdecff714e9a2b4995b5e0337a6046f440a91485a1035b2de2a7d242670`.

Gli intervalli DST sono una fotografia delle regole Windows del 24/09/2026: **inizio incluso, fine esclusa**, senza sovrapposizioni. L'offset comprende già l'ora legale; India `5.5`. IANA e Windows restano disponibili per il futuro mini-tool. Cambiamenti normativi successivi richiedono una nuova esportazione.

## Calendari e precedenze

| Informazione | Effetto |
| --- | --- |
| Santo o ricorrenza religiosa | Solo informazione |
| Festività nazionale o patrono applicabile | Giorno locale non lavorativo |
| Weekend/venerdì libero | Regola settimanale della singola colonna |
| Ferie, chiusure aziendali, recuperi personali | **Eccezioni**, con precedenza |

Ordine effettivo: **eccezione personale → festività → recupero nazionale → riposo settimanale → orari**. Gli orari e i margini continuano a valere quando un recupero riapre un giorno. Due eccezioni attive sovrapposte sulla stessa colonna/data producono un avviso, senza scegliere arbitrariamente quale vinca.

`Active=0` disabilita una riga. Con `Active=1`, `Kind=HOLIDAY` chiude il giorno; `Kind=WORKDAY` annulla il riposo settimanale ma non una festività esplicita o un'eccezione personale. `Status`, `Version`, `Updated` e `Source` descrivono provenienza e attendibilità della snapshot.

La base deriva da [python-holidays 0.105](https://github.com/vacanza/holidays/tree/v0.105), licenza MIT; gli URL per paese sono in ogni riga. Sono inclusi CN, IN, KR, JP, BD, IT, FR, NO, SE, US, CA per 2026–2028. Non sono dati verificati singolarmente contro tutti i decreti ufficiali.

- **USA e Canada:** base federale, non un calendario obbligatorio uniforme per ogni impresa; nessuno stato/provincia selezionato.
- **India:** base nazionale della libreria, non tutti i calendari religiosi/regionali o aziendali.
- **Svezia:** escluse le domeniche generiche, gestite dalla regola settimanale; comprese le ricorrenze `public` e `de_facto` della fonte.
- **Cina:** sei recuperi del 2026 disponibili; ponti/recuperi 2027–2028 non definitivi e marcati provvisori.
- Date mobili stimate della fonte sono marcate. Non trasformare una data stimata in una certezza operativa.
- Conservate 24 date GOV.UK per **Inghilterra e Galles**. VN/BY sono ancora senza dati.

In **Calendari A6:F25** registri i codici e la copertura: `Dati=1` significa dati presenti, non certificati. La tabella manuale sotto resta separata dal CSV e sopravvive al refresh. Aggiungi lì un patrono al codice applicabile. Un nuovo codice `IT-ROMA` non eredita automaticamente le date di `IT`.

**Eccezioni:** colonna 1–7, date locali Dal/Al comprese, `Riposo` o `Attivita`, Attiva=1. La tabella ha 100 righe predisposte ed è estendibile come tabella Excel. I venerdì liberi si impostano in **Parametri**, senza generare centinaia di festività.

## Santi e Luna

I santi sono ricorrenze fisse nominali: 191 date, 211 voci, 175 date ricorrenti mancanti su 366. Fonte principale [LiturgicalCalendarAPI](https://github.com/Liturgical-Calendar/LiturgicalCalendarAPI), revisione e Apache-2.0 conservati in `calendari-2026-09-24/saints-agent`. Conservati i due esempi precedenti con link Vatican News; nessuna biografia copiata. Nessun dato Wikidata utilizzato.

Non è un martirologio completo, né gestisce precedenze/trasferimenti liturgici annuali. Mancanza della riga = informazione non caricata. I santi si riferiscono alla **data di casa** e non cambiano disponibilità o festività.

Luna: [USNO](https://aa.usno.navy.mil/data/MoonPhases), [API](https://aa.usno.navy.mil/data/api). `Utc` indica l'istante della fase primaria, `Between` il tratto successivo. Il planner mostra fase all'inizio della vista e prossimo evento UTC. Non calcola illuminazione, levata/tramonto o orientamento. Aggiorna tutto non scarica nuove annate.

## Meteo OpenWeather

**Parametri B32 vuota:** Power Query legge `Meteo.csv`. Lo script esterno `weather-agent/fetch_weather.py` può aggiornarlo usando chiave da ambiente o dal file fornito; cache 30 minuti, numero richieste limitato. Questa copia iniziale contiene sette risposte reali, senza errori.

**B32 compilata:** la query `Weather` legge i codici distinti dalle sette colonne, associa coordinate del catalogo e usa [Current Weather API](https://openweathermap.org/api/current). Dati → Aggiorna tutto richiede il meteo attuale in °C, descrizione italiana, vento m/s. Non salva le nuove risposte nel CSV e non usa la data pianificata come data di previsione.

Prima connessione: origine `https://api.openweathermap.org`, accesso **Anonimo** perché la chiave è passata dalla cella come `appid`. Il valore è salvato in chiaro nel workbook se lo salvi: non condividere quella copia. La chiave demo non è incorporata nel file consegnato, nei sorgenti o nei report.

Su richiesta del proprietario è consentito **Fast Combine per questo workbook**. Lo script `Apri-Planner.ps1` lo abilita nella sessione aperta. Per conservarne l'impostazione tramite Excel: **Dati → Recupera dati → Opzioni query → Cartella di lavoro corrente → Privacy → Ignora i livelli di privacy**. Non serve cambiare l'opzione globale. [Microsoft](https://learn.microsoft.com/en-us/power-query/privacy-levels).

`ObservedUtc` è l'osservazione; `FetchedUtc` il download. Il timestamp è mostrato anche sotto le città. `stale:*` segnala dati conservati dopo errore; `aged_observation` un'osservazione vecchia. Nessuna chiave o URL autenticata nelle celle di output. La query produce una richiesta logica per città distinta; anteprime o rivalutazioni di Excel possono influire sul numero reale di chiamate. Il limite dell'account resta quello del provider.

## Modificare i dati

- Usa un editor di testo o **Dati → Da testo/CSV**: il doppio clic può convertire codici/date.
- Non editare le tabelle importate in Da_file/Fusi/Info: vengono sostituite al refresh. Usa Calendari/Eccezioni per i dati personali.
- Tieni workbook e Calendar data insieme; aggiorna B25 quando li sposti.
- Script, prove, licenze, fonti e originali non sono dipendenze di esecuzione dell'Excel. Nessuna skyline, immagine raster o bandiera è stata aggiunta in questo round.
