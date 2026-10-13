export async function downloadFile(fileName, streamReference) {
    const buffer = await streamReference.arrayBuffer();
    const blob = new Blob([buffer], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    try {
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
    } finally {
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
}
