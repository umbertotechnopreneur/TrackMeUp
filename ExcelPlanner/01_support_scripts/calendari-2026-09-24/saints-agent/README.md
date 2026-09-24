# Santi: dataset informativo in italiano

`Santi.csv` contiene **191 date e 211 voci**, distribuite nei 12 mesi. È un
sottoinsieme a data fissa del santorale romano generale, con ricorrenze mariane,
commemorazioni collettive e angeli. **Non è il martirologio completo** e non
determina festività civili, chiusure o giorni lavorativi.

Formato: UTF-8 con BOM, delimitatore `;`, intestazione esatta
`Month;Day;Name;Source`. Ogni coppia mese/giorno coperta compare una sola volta,
in ordine cronologico. Più voci nello stesso giorno sono unite con `; ` dentro
un campo `Name` quotato. Anche `Source`, se contiene più URL, è quotato.
Usare un lettore CSV con delimitatore `;` e qualificatore di testo `"`:
non dividere le righe con un semplice `split(';')`.

Sono conservati gli esempi del proprietario: 24 settembre, Beata Vergine Maria
della Mercede; 4 ottobre, San Francesco d’Assisi, con i rispettivi URL Vatican
News. Non sono state copiate biografie.

## Copertura e limiti

| Mese | Date coperte |
| --- | ---: |
| Gennaio | 13 |
| Febbraio | 12 |
| Marzo | 8 |
| Aprile | 13 |
| Maggio | 16 |
| Giugno | 16 |
| Luglio | 20 |
| Agosto | 23 |
| Settembre | 19 |
| Ottobre | 18 |
| Novembre | 15 |
| Dicembre | 18 |

Mancano **175 delle 366 possibili date ricorrenti**, incluso il 29 febbraio:
nel 2026 sono 174 giorni senza voce. I giorni mancanti sono assenti dal CSV;
non significano «nessun santo». L'elenco completo è in
`manifest.json → coverage.missing_dates_mm_dd`.

Le date sono quelle nominali ricorrenti. Non vengono applicate precedenze,
soppressioni domenicali o trasferimenti per un singolo anno. Sono escluse
ricorrenze mobili, calendari locali/diocesani, dedicazioni di basiliche e alcune
feste non riferite a santi. La revisione della fonte è fissata; il dataset non
si aggiorna automaticamente. I gruppi già presenti in una singola etichetta
della fonte restano integri: 211 voci non significa 211 singole persone.

## Fonti e riproduzione

Fonte principale: [LiturgicalCalendarAPI](https://github.com/Liturgical-Calendar/LiturgicalCalendarAPI),
revisione `1bb2b7c503a701a9713b2f881795afe46044af3b`, licenza Apache-2.0.
Leggere `LICENSE-PROVENANCE.md` per attribuzione, modifiche e distinzione tra
dati aperti e i due esempi forniti dal proprietario.

Da questa cartella, ricostruzione offline con Python standard, senza dipendenze:

```powershell
python -B ./fetch_saints.py --build
```

Per riscaricare le fonti fissate, usare lo stesso comando con `--fetch-litcal`,
poi `--build`. I file già presenti e verificati tramite SHA-256 vengono
riutilizzati. Un file alterato causa un errore, non viene sovrascritto.
Tutti gli output sono relativi alla cartella dello script; nessun workbook,
dato Calendar o altro script viene letto o modificato.

`raw/` conserva JSON originali, traduzioni, licenza e prova della correzione
permanente di Giovanna Francesca de Chantal dal 12 dicembre al 12 agosto.
`raw/sources.json` contiene URL fissati, data di acquisizione e SHA-256.
`provenance.json` collega ogni voce a chiavi e file originali.
`manifest.json` conserva copertura, esclusioni, modifiche e verifiche.

Wikidata era la fonte preferita, ma la richiesta registrata è terminata per
timeout, dopo il precedente HTTP 429 segnalato dal proprietario. Nessun dato
Wikidata è stato usato nel CSV. La query è conservata in `wikidata-saints.rq`
e l'esito in `raw/wikidata-request.json`. L'opzione esplicita
`--fetch-wikidata` esegue una sola richiesta, senza ritenti, e non è necessaria
per la ricostruzione. Rispettare eventuale `Retry-After` prima di un futuro
tentativo manuale; la query conservata richiede ulteriore verifica dei
qualificatori religiosi e del calendario prima di usare i suoi risultati.
