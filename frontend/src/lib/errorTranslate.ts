export function translateError(error: string): string {
    let output = "Ein Fehler ist aufgetreten!";
    switch (error) {
        case "Transactions are currently disabled. [FULL LOAD REJECTION]":
            output = "Transaktionen sind derzeit deaktiviert. [FULL_LOAD_REJECTION]";
        break;
        case "Insufficient funds.":
            output = "Nich genügend Guthaben.";
        break;
        case "Internal server error while processing transaction.":
            output = "Interner Serverfehler bei der Überweisung. Bitte melde diesen Fehler der Bankzentrale. U_U";
        break;
    }
	return output;
}