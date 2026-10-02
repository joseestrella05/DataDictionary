window.downloadFileFromUrl = function (url) {
    const link = document.createElement('a');
    link.href = url;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.downloadFileFromBase64 = function (fileName, base64Data, contentType) {
    const byteCharacters = atob(base64Data);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: contentType || 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' });

    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(link.href);
};

// Update the navbar database connection indicator
window.setNavDbStatus = function (dbName, isConnected) {
    const dot   = document.getElementById('nav-db-dot');
    const label = document.getElementById('nav-db-label');
    if (!dot || !label) return;

    if (isConnected && dbName) {
        dot.className = 'nav-db-dot active';
        label.textContent = dbName;
        label.style.color = '';
        label.style.fontWeight = '500';
    } else {
        dot.className = 'nav-db-dot idle';
        label.textContent = 'Sin conexión';
        label.style.color = 'var(--clr-text-subtle)';
        label.style.fontWeight = '400';
    }
};
