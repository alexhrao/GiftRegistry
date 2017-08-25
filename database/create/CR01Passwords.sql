CREATE TABLE gift_registry_db.passwords (
    PasswordID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    UserID INT NOT NULL,
    PasswordHash CHAR(28) NOT NULL,
    PasswordSalt CHAR(24) NOT NULL,
    PasswordIter INT UNSIGNED NOT NULL,
    CreateStamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);