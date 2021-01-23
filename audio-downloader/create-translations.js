const fs = require("fs");
const exec = require("child_process").execSync;

const data = {
    "ACCEPT_AGREEMENT": "If you accept the terms of the agreement, click I Agree to continue. You must accept the agreement to install the Esportal Client.",
    "BROWSE": "browse",
    "BUTTON_AGREE": "I Agree",
    "BUTTON_BACK": "Back",
    "BUTTON_INSTALL": "Install",
    "BUTTON_NEXT": "Next",
    "CHECK_COMPONENTS": "Check the components you want to install and uncheck the components you don't want to install.",
    "CLICK_NEXT": "Click Next to continue.",
    "COMPLETED": "Completed",
    "CONFIRM_INSTALL_DIR": "Directory already exists. Install in this location?",
    "COPIED": "copied",
    "DELETED": "Deleted",
    "DELETE_FAIL": "failed to delete",
    "DESCRIPTION": "Description",
    "DESTINATION_FOLDER": "Destination Folder",
    "EXISTING_INSTALL": "Existing installation detected. Continue to overwrite, or uninstall first.",
    "FINISH": "Finish",
    "INSTALL_WELCOME_LINE_1": "Setup will guide you through the installation of the Esportal Client.",
    "INSTALL_WELCOME_LINE_2": "It is recommended that you close all other applications before starting setup. This will make it possible to update relevant system files without having to reboot your computer.",
    "NO_DIR_PERMISSION": "You do not have permission to write to that directory.",
    "SCROLLDOWN_AGREEMENT": "Press page down to see the rest of the agreement",
    "SEE_DESCRIPTION": "Position your mouse over component to see description.",
    "SELECT_COMPONENTS": "Select components to install:",
    "SELECT_LANG": "Please select a language",
    "SELECT_VALID_INSTALL_DIR": "Select a valid install location",
    "SPACE _AVAILABLE": "Space available:",
    "SPACE_REQUIRED": "Space required:",
    "UNINSTALL": "Uninstall",
    "UNINSTALL_FROM": "Uninstall from:",
    "UNINSTALL_LOCATION_HELP": "Esportal will be uninstalled from the following folder. Click Uninstall to start the uninstallation",
    "UNINSTALL_WELCOME_LINE_1": "Setup will guide you through the uninstallation of the Esportal Client.",
    "UNINSTALL_WELCOME_LINE_2": "Before starting the uninstallation, make sure Esportal Client is not running.",
    "WINDOW_TITLE_INSTALLER": "Esportal Installer",
    "WINDOW_TITLE_UNINSTALLER": "Esportal Uninstaller"
}

var langs = [
    "sv",
    "fi",
    "da",
    "no",
    "pt",
    "es",
    "fr",
    "pl",
    "ru",
    "de",
    "uk",
];


const keys = Object.keys(data);


let arg  = "";
for (const k of keys) {
    arg += data[k]+"\n";
}

for (const lang of langs) {
    const output = exec('trans -b en:'+lang+' "'+arg+'"', {encoding: 'utf8'});

    let i = 0;
    let output2 = "module.exports = {\n";
    for (const line of output.split("\n")) {
        if (!line.trim()) {
            continue;
        }
        const k = keys[i++];
        output2 += `"${k}": "${line}",\n`;
    }
    output2 += "}";
    console.log(output2);
    fs.writeFileSync(lang+".lang.js", output2);
    exec("sleep 10");
}


