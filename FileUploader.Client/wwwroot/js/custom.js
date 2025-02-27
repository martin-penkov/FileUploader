window.downloadMyFile = (fileBytes, fullFileName) => {
    let blob = new Blob([new Uint8Array(fileBytes)], { type: "application/octet-stream" });
    let url = URL.createObjectURL(blob);

    let a = document.createElement("a");
    a.href = url;
    a.download = fullFileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);

    URL.revokeObjectURL(url);
}