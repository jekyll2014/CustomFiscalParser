# CustomFiscalParser
CUSTOM Fiscal printers command parser (for RUSSIA)

Can decode commands and replies to/from CUSTOM fiscal printers made for RUSSIA (for example Q3X-Ф).
Command and errors databases are stored into .CSV files.

Parameter types available:
 - password - contains operator number and password (nn pppp)
 - string - printable string data
 - number - int number
 - money - double data formed to xxxxxxx.yy
 - quantity - goods quantity adopted to weight definition - formed to xxxxx.yyy
 - error# - error number for fiscal controller replies. Decoded according to errors database.
 - data - non-printable data
 - bitfield - byte divided into bit flags.

any other types will be treated as mistakes and only RAW values displayed.
