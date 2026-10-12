#pragma once
#include <windows.h>
#include <algorithm>

struct installerLayout { RECT label, folder, browse, paths, status, progress, details, install, cancel; };
inline installerLayout calculateLayout(int width, int height, unsigned dpi) {
    auto u = [=](int n) { return MulDiv(n, static_cast<int>(dpi), 96); };
    auto rect = [](int x, int y, int w, int h) { return RECT{x, y, x + (std::max)(0, w), y + (std::max)(0, h)}; };
    int margin = u(16), gap = u(8), button = u(34); bool wide = width >= u(584);
    int footer = (std::max)(0, height - margin - (wide ? button : button * 3 + gap * 2));
    int bar = (std::max)(0, footer - gap - u(22)); int bodyBottom = (std::max)(0, bar - gap);
    bool compact = bodyBottom < u(210); int folderTop = compact ? margin : u(48), bodyTop = folderTop + u(30) + gap;
    int bodyHeight = (std::max)(0, bodyBottom - bodyTop), pathsHeight = bodyHeight / 2;
    installerLayout r{};
    r.label = rect(margin, u(16), width - margin * 2, compact ? 0 : u(24));
    r.folder = rect(margin, folderTop, width - margin * 2 - u(100) - gap, u(30)); r.browse = rect(width - margin - u(100), folderTop, u(100), u(30));
    r.paths = rect(margin, bodyTop, width - margin * 2, pathsHeight); r.status = rect(margin, bodyTop + pathsHeight + gap, width - margin * 2, bodyHeight - pathsHeight - gap);
    r.progress = rect(margin, bar, width - margin * 2, u(22));
    if (wide) {
        r.details = rect(margin, footer, u(144), button); r.cancel = rect(width - margin - u(108), footer, u(108), button);
        r.install = rect(r.cancel.left - gap - u(260), footer, u(260), button);
    } else {
        r.details = rect(margin, footer, width - margin * 2, button); r.install = rect(margin, footer + button + gap, width - margin * 2, button);
        r.cancel = rect(margin, footer + (button + gap) * 2, width - margin * 2, button);
    }
    return r;
}
inline RECT fitWorkArea(RECT proposed, const RECT& work) {
    int width = (std::min)(proposed.right - proposed.left, work.right - work.left), height = (std::min)(proposed.bottom - proposed.top, work.bottom - work.top);
    int x = (std::max)(work.left, (std::min)(proposed.left, work.right - width)), y = (std::max)(work.top, (std::min)(proposed.top, work.bottom - height));
    return {x, y, x + width, y + height};
}
