# Round del 24 settembre 2026

Il file operativo resta due cartelle sopra: `Planner_Disponibilita_A4.xlsx`, con i sei CSV in `Calendar data`. Questa cartella contiene solo sorgenti, provenienza e verifiche.

## Risultato

- Copertina informativa A4, planner A4, vista estesa su due A4.
- 11 calendari nazionali 2026–2028, recuperi cinesi e precedenze per eccezioni personali.
- 211 voci di santi su 191 date, informative e non complete.
- Meteo CSV pre-caricato; campo OpenWeather facoltativo in Parametri B32, lasciato vuoto.
- Sei query native conservate. Verificate sia la lettura CSV sia la richiesta API da Excel per sette città.
- 46 verifiche native sulle regole: festività, margini, notte, weekend, recuperi, deroghe, sovrapposizioni e conservazione degli input.
- Nessuna modifica al repository WorkTrail, al DB o alle skyline. Nessun asset raster aggiunto.

## Sorgenti

- `prepare-datasets.py`, `dati_preparati`, `holiday-manifest.json`, `vendor`: generazione calendari con holidays 0.105 e relativa licenza MIT.
- `saints-agent`: ricorrenze, script, fonti grezze, manifest e Apache-2.0 della fonte LiturgicalCalendarAPI.
- `weather-agent`: esportazione città dal DB in sola lettura, fetcher con cache, query M e documentazione OpenWeather.
- `cover-agent`: sorgente `.mjs`, workbook donatore e anteprima della copertina.
- `build-revision.mjs`, `apply-revision.ps1`: generazione Eccezioni e migrazione nativa che preserva Power Query. Migrazione una tantum dello schema precedente, non da rilanciare sulla versione aggiornata.
- `verify-scenarios.ps1`, `verify-weather-api.ps1`, `polish-staged.ps1`, `verifiche`: controlli e anteprime; non servono per usare il planner.
- `publish-verified.ps1`: pubblicazione esplicita, con verifica hash e backup. Non ripetere dopo la pubblicazione.
- `Apri-Planner.ps1`: apertura facoltativa con Fast Combine per la sessione del solo workbook. Non aggiorna automaticamente i dati.

Le impostazioni di privacy globali non sono cambiate. Fast Combine è stato autorizzato dal proprietario e verificato sul workbook. La chiave non è salvata nei file consegnati: la prova API usa solo modifiche in memoria poi scartate.

Le festività sono una base derivata da regole, non una certificazione ufficiale per ogni azienda. Stati/province esclusi; CN 2027–2028 e date stimate richiedono aggiornamento. Vietnam/Bielorussia restano senza calendario. Santi: mancano 175 date ricorrenti su 366; nessun trasferimento liturgico annuale.

Le anteprime PDF sono state esportate con Excel nativo: Planner 1 A4 orizzontale, Estesa 2 A4 orizzontali, Copertina 1 A4 verticale. Le immagini PNG in verifiche sono soltanto controlli visivi, non asset incorporati nel workbook.
