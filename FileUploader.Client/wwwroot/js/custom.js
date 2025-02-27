window.downloadMyFile = (fileBytes, fileName) => {
    let blob = new Blob([new Uint8Array(fileBytes)], { type: "application/octet-stream" });
    let url = URL.createObjectURL(blob);

    let a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);

    URL.revokeObjectURL(url);
}