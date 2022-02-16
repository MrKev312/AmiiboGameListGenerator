# AmiiboGameListGenerator
This project is created to supply the [AmiiboAPI](https://github.com/N3evin/AmiiboAPI "AmiiboAPI") with a games_info.json file. This file is used to store which Amiibo can be used in which game and in what way.

## Usage
Use `-h` or `-help` to see the help message.  
Use `-i {path}` or `-input {path}` to specify the input json location.  
Use `-o {path}` or `-output {path}` to specify the output json location.  
Use `-u` or `-update` to automatically get the [latest amiibo.json](https://raw.githubusercontent.com/N3evin/AmiiboAPI/master/database/amiibo.json "latest amiibo.json") from github. When combined with `-i`, the file will be stored at that location.  
Use `-l {value}` or `-log {value}` to set the logging level, can pick from verbose, info, warn, error or from 0 to 3 respectively.
