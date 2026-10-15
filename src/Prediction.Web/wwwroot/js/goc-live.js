// Góc Live: tiếng "ting" và thông báo trình duyệt khi có khuyến nghị góc mới.
window.gocLive = {
    permission: function () {
        return "Notification" in window ? Notification.permission : "unsupported";
    },
    requestPermission: async function () {
        if (!("Notification" in window)) return "unsupported";
        return await Notification.requestPermission();
    },
    beep: function () {
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            [880, 1320].forEach(function (freq, i) {
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = "sine";
                osc.frequency.value = freq;
                const start = ctx.currentTime + i * 0.18;
                gain.gain.setValueAtTime(0.0001, start);
                gain.gain.exponentialRampToValueAtTime(0.3, start + 0.02);
                gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.35);
                osc.connect(gain).connect(ctx.destination);
                osc.start(start);
                osc.stop(start + 0.4);
            });
        } catch (e) { /* trình duyệt chặn âm thanh khi chưa tương tác */ }
    },
    scrollTo: function (id) {
        const el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: "smooth" });
    },
    notify: function (title, body) {
        if ("Notification" in window && Notification.permission === "granted") {
            try { new Notification(title, { body: body, tag: title }); } catch (e) { }
        }
    }
};
