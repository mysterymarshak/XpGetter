# XpGetter
### CLI tool for retrieving your CS2 weekly drop information

**Usage Demo:**  
![XpGetter_demo](https://github.com/user-attachments/assets/625dd22e-e412-4d46-b542-7fa3d9571267)

## How to use

### Add your Steam account
You can add your Steam account using your username/password or by scanning a QR code via the Steam Guard app  
**Example of adding an account via QR Code:**  
![xpgetter_qr_code_demo](https://github.com/user-attachments/assets/6f468a65-890b-435b-b120-d7c6e01c7ea7)

And that's it!  
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
* **Errors/Warnings:** `:( ` or `-_-;`

If you encounter non-OK statuses, check the log files automatically generated in the same folder as the executable

### 4. The Result
In this section, you can see the "activity info" for each saved account  
The output is formatted to be easily readable (but I have plans to add custom formatting)

## Installation

Please keep this tool in its own dedicated folder (e.g., `/XpGetter`)  
This is required because the application stores `configuration` files and logs next to the executable  
This allows you to easily remove the program completely from your machine so no trash in unknown places

### Framework Dependent
If you already have the [dotnet 10 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed on your machine, download this version to reduce the file size

### Self Contained
This build doesn't require a .NET installation on your machine  

**To start use:** Extract the contents of the `.zip` release and run the executable inside the `/XpGetter` folder
