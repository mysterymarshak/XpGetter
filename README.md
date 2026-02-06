# XpGetter
### CLI tool for retrieving your CS2 weekly drop information & statistics

**Usage Demo:**  
![XpGetter_demo_activity_info](https://github.com/user-attachments/assets/625dd22e-e412-4d46-b542-7fa3d9571267)

Since version `0.2.0` you can also see all of your new rank drops by some period (30, 90, 180, 365 days)
![xpgetter_demo_statistics](https://github.com/user-attachments/assets/c069b48e-b5c9-44f7-974e-10a641655165)

## How to use

### Add your Steam account
You can add your Steam account using your username/password or by scanning a QR code via the Steam Guard app  
**Example of adding an account via QR Code:**  
![xpgetter_qr_code_demo](https://github.com/user-attachments/assets/6f468a65-890b-435b-b120-d7c6e01c7ea7)

**And that's it!**  
You will now be able to "Get activity info" for your accounts  
You can add multiple of them (up to `5`)

## A Closer Look
Let's look at the output in detail:  
<img width="695" height="821" alt="xpgetter_details" src="https://github.com/user-attachments/assets/6ad56db1-1e27-46dc-b881-8e3d43751ae8" />

### 1. Program Version
XpGetter doesn't auto-update, so you should manually download new versions from the **Releases** page  
To quickly check for updates, use the "Check for updates" section

### 2. Saved Accounts
XpGetter will retrieve weekly drop information for these accounts  
To manage them (add new or remove old ones), go to the "Manage accounts" section

### 3. Activity Status
This section shows what the application is processing in real-time  
* **Success:** `:)`
* **Errors/Warnings:** `:( ` / `-_-;`

If you encounter non-OK statuses, check the log files automatically generated in the same folder as the executable

### 4. The Result
In this section, you can see the "activity info" for each saved account  
The output is formatted to be easily readable (but I have plans to add custom formatting)

## Statistics
You can get your new rank drops statistics by some period for all of your accounts!  
> [!WARNING]
> Don't do that multiple times in a row, otherwise you will get `429 (Too many requests)` error  
> And it may also happen if your inventory history (trades, market operations) is very long and you seek it for like 3-5 accounts for 365 days  
> But even in case of `429` error `XpGetter` will show results for you after a bit of waiting

> [!TIP]
> If you got `TooLongHistory` result see [--no-page-limit](#flags) flag

The real demo of 90 days statistics:
<img width="2022" height="588" alt="image" src="https://github.com/user-attachments/assets/059d2a99-d425-4b58-a2c4-f994b065ed36" />

## Flags (parameters)
You can get all of the actual parameters with their descriptions by running `XpGetter` with `--help` argument  
| Argument | Default | Description |
| :--- | :---: | :--- |
| `--skip-menu` | — | Skips the menu and immediately starts the activity info retrieving |
| `--censor` | `True` | Censors usernames in terminal output. (Note: logs remain uncensored) |
| `--anonymize` | — | Anonymizes all usernames/nicknames in terminal output. (Note: logs remain unanonymized) |
| `--dont-use-currency-symbols` | — | Replaces symbols (e.g., `$`) with ISO codes (e.g., `USD`) |
| `--currency` | — | Overrides the currency for all price requests |
| `--price-provider` | `Steam` | Sets the provider used for item price fetching |
| `--no-page-limit` | - | Ignores inventory history items limit (`150` by default) to receive (use *only* if you really need that) |

Some usage examples:
```
./XpGetter --skip-menu
./XpGetter --anonymize --currency USD
./XpGetter --censor false
./XpGetter --price-provider MarketCsgo --currency RUB --dont-use-currency-symbols
```

## Installation
Go to [Releases](https://github.com/mysterymarshak/XpGetter/releases) page and download the latest version based on your system and dotnet installation:

**Framework Dependent**  
If you already have the [dotnet 10 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed on your machine, download this version to reduce the file size

**Self Contained**  
This build doesn't require a .NET installation on your machine  
\
**To start use:** Extract the contents of the `.zip` release and run the executable inside the `/XpGetter` folder

> [!IMPORTANT]
> Please keep this tool in its own dedicated folder (e.g., `/XpGetter`)  
> This is required because the application stores `configuration` files and logs next to the executable  
> This allows you to easily remove the program completely from your machine so no trash in unknown places  

## Build from sources
Clone the repo and cd:
```
git clone git@github.com:mysterymarshak/XpGetter.git
cd XpGetter
```

Then depends on your system and dotnet installation run:

### Linux
Framework Dependent build:
```
dotnet publish -p:PublishSingleFile=true -p:SelfContained=false -r linux-x64 -p:EnableCompressionInSingleFile=false src/XpGetter.Cli/XpGetter.Cli.csproj -p:DebugType=None -p:DebugSymbols=false -o artifacts/
```
Self Contained build:
```
dotnet publish -p:PublishSingleFile=true -p:SelfContained=true -r linux-x64 -p:EnableCompressionInSingleFile=true src/XpGetter.Cli/XpGetter.Cli.csproj -p:DebugType=None -p:DebugSymbols=false -o artifacts/
```
### Windows
Framework Dependent build:
```
dotnet publish -p:PublishSingleFile=true -p:SelfContained=false -r win-x64 -p:EnableCompressionInSingleFile=false src/XpGetter.Cli/XpGetter.Cli.csproj -p:DebugType=None -p:DebugSymbols=false -o artifacts/
```
Self Contained build:
```
dotnet publish -p:PublishSingleFile=true -p:SelfContained=true -r win-x64 -p:EnableCompressionInSingleFile=true src/XpGetter.Cli/XpGetter.Cli.csproj -p:DebugType=None -p:DebugSymbols=false -o artifacts/
```
\
You will find your executables in `artifacts` folder:
```
cd artifacts
```

## Known issues

* **Currency Encoding (Windows):** If you are seeing `?` symbols instead of currency icons, you can use the `--dont-use-currency-symbols` flag. It replaces symbols with text codes (e.g., `USD`)
* **Caching:** Some data may be cached to improve performance, this mechanism is planned for a future updates
* **Statistics Scaling:** Resize your terminal **before** retrieving statistics. The layout is rendered based on the terminal's w/h at the moment of printing; stretching the window after rendering will not adjust the layout
