// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;

namespace NSail.Components;

/// <summary>Icon catalog for the generic UI vocabulary: pages and kits name icons through
/// this class, never through the vendor's. Products add their own catalog for domain icons.</summary>
// Material Symbols (Apache 2.0), drawn at the 20px optical size rather than a scaled 24.
// Symbols ships a "0 -960 960 960" viewBox, and an icon reaches the screen through whichever
// control was given it (a button's StartIcon, a nav link, a field adornment) — each renders
// its own svg fixed at "0 0 24 24", and none of them asks. So every entry carries the mapping
// itself: scale by 24/960 and drop back into the visible quadrant. Self-contained, one line,
// identical everywhere. Paste new icons the same way.
public static class NsIcons
{
    /// <summary>Material Symbols <c>home</c>.</summary>
    public static readonly Glyph Home =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M264-216h96v-240h240v240h96v-348L480-726 264-564v348Zm-72 72v-456l288-216 288 216v456H528v-240h-96v240H192Zm288-327Z"/></g>""");

    /// <summary>Material Symbols <c>search</c>.</summary>
    public static readonly Glyph Search =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M765-144 526-383q-30 22-65.79 34.5-35.79 12.5-76.18 12.5Q284-336 214-406t-70-170q0-100 70-170t170-70q100 0 170 70t70 170.03q0 40.39-12.5 76.18Q599-464 577-434l239 239-51 51ZM384-408q70 0 119-49t49-119q0-70-49-119t-119-49q-70 0-119 49t-49 119q0 70 49 119t119 49Z"/></g>""");

    /// <summary>Material Symbols <c>add</c>.</summary>
    public static readonly Glyph Add =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M444-444H240v-72h204v-204h72v204h204v72H516v204h-72v-204Z"/></g>""");

    /// <summary>Material Symbols <c>edit</c>.</summary>
    public static readonly Glyph Edit =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-216h51l375-375-51-51-375 375v51Zm-72 72v-153l498-498q11-11 23.84-16 12.83-5 27-5 14.16 0 27.16 5t24 16l51 51q11 11 16 24t5 26.54q0 14.45-5.02 27.54T795-642L297-144H144Zm600-549-51-51 51 51Zm-127.95 76.95L591-642l51 51-25.95-25.05Z"/></g>""");

    /// <summary>Material Symbols <c>delete</c>.</summary>
    public static readonly Glyph Delete =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M312-144q-29.7 0-50.85-21.15Q240-186.3 240-216v-480h-48v-72h192v-48h192v48h192v72h-48v479.57Q720-186 698.85-165T648-144H312Zm336-552H312v480h336v-480ZM384-288h72v-336h-72v336Zm120 0h72v-336h-72v336ZM312-696v480-480Z"/></g>""");

    /// <summary>Material Symbols <c>save</c>.</summary>
    public static readonly Glyph Save =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M816-672v456q0 29.7-21.15 50.85Q773.7-144 744-144H216q-29.7 0-50.85-21.15Q144-186.3 144-216v-528q0-29.7 21.15-50.85Q186.3-816 216-816h456l144 144Zm-72 30L642-744H216v528h528v-426ZM480-252q45 0 76.5-31.5T588-360q0-45-31.5-76.5T480-468q-45 0-76.5 31.5T372-360q0 45 31.5 76.5T480-252ZM264-552h336v-144H264v144Zm-48-77v413-528 115Z"/></g>""");

    /// <summary>Material Symbols <c>close</c>.</summary>
    public static readonly Glyph Close =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="m291-240-51-51 189-189-189-189 51-51 189 189 189-189 51 51-189 189 189 189-51 51-189-189-189 189Z"/></g>""");

    /// <summary>Material Symbols <c>check</c>.</summary>
    public static readonly Glyph Check =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M389-267 195-460l51-52 143 143 325-324 51 51-376 375Z"/></g>""");

    /// <summary>Material Symbols <c>settings</c>.</summary>
    public static readonly Glyph Settings =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="m403-96-22-114q-23-9-44.5-21T296-259l-110 37-77-133 87-76q-2-12-3-24t-1-25q0-13 1-25t3-24l-87-76 77-133 110 37q19-16 40.5-28t44.5-21l22-114h154l22 114q23 9 44.5 21t40.5 28l110-37 77 133-87 76q2 12 3 24t1 25q0 13-1 25t-3 24l87 76-77 133-110-37q-19 16-40.5 28T579-210L557-96H403Zm59-72h36l19-99q38-7 71-26t57-48l96 32 18-30-76-67q6-17 9.5-35.5T696-480q0-20-3.5-38.5T683-554l76-67-18-30-96 32q-24-29-57-48t-71-26l-19-99h-36l-19 99q-38 7-71 26t-57 48l-96-32-18 30 76 67q-6 17-9.5 35.5T264-480q0 20 3.5 38.5T277-406l-76 67 18 30 96-32q24 29 57 48t71 26l19 99Zm18-168q60 0 102-42t42-102q0-60-42-102t-102-42q-60 0-102 42t-42 102q0 60 42 102t102 42Zm0-144Z"/></g>""");

    /// <summary>Material Symbols <c>palette</c>.</summary>
    public static readonly Glyph Palette =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-96q-79 0-149-30t-122.5-82.5Q156-261 126-331T96-480q0-80 30.5-149.5t84-122Q264-804 335.5-834T488-864q78 0 146.5 27T754-763q51 47 80.5 110T864-518q0 96-67 163t-163 67h-68q-8 0-14 5t-6 13q0 15 15 25t15 53q0 37-27 66.5T480-96Zm0-384Zm-216 36q25 0 42.5-17.5T324-504q0-25-17.5-42.5T264-564q-25 0-42.5 17.5T204-504q0 25 17.5 42.5T264-444Zm120-144q25 0 42.5-17.5T444-648q0-25-17.5-42.5T384-708q-25 0-42.5 17.5T324-648q0 25 17.5 42.5T384-588Zm192 0q25 0 42.5-17.5T636-648q0-25-17.5-42.5T576-708q-25 0-42.5 17.5T516-648q0 25 17.5 42.5T576-588Zm120 144q25 0 42.5-17.5T756-504q0-25-17.5-42.5T696-564q-25 0-42.5 17.5T636-504q0 25 17.5 42.5T696-444ZM480-168q11 0 17.5-8.5T504-192q0-16-15-28t-15-50q0-38 26.5-64t64.5-26h69q66 0 112-46t46-112q0-115-88.5-194.5T488-792q-134 0-227 91t-93 221q0 130 91 221t221 91Z"/></g>""");

    /// <summary>Material Symbols <c>image</c>.</summary>
    public static readonly Glyph Image =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-144q-29.7 0-50.85-21.5Q144-187 144-216v-528q0-29 21.15-50.5T216-816h528q29.7 0 50.85 21.5Q816-773 816-744v528q0 29-21.15 50.5T744-144H216Zm0-72h528v-528H216v528Zm48-72h432L552-480 444-336l-72-96-108 144Zm-48 72v-528 528Z"/></g>""");

    /// <summary>Material Symbols <c>language</c>.</summary>
    public static readonly Glyph Language =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-96q-79 0-149-30t-122.5-82.5Q156-261 126-331T96-480q0-80 30-149.5t82.5-122Q261-804 331-834t149-30q80 0 149.5 30t122 82.5Q804-699 834-629.5T864-480q0 79-30 149t-82.5 122.5Q699-156 629.5-126T480-96Zm0-75q17-17 34-63.5T540-336H420q9 55 26 101.5t34 63.5Zm-91-10q-14-30-24.5-69T347-336H204q29 57 77 97.5T389-181Zm182 0q60-17 108-57.5t77-97.5H613q-7 47-17.5 86T571-181ZM177-408h161q-2-19-2.5-37.5T335-482q0-18 .5-35.5T338-552H177q-5 19-7 36.5t-2 35.5q0 18 2 35.5t7 36.5Zm234 0h138q2-20 2.5-37.5t.5-34.5q0-17-.5-35t-2.5-37H411q-2 19-2.5 37t-.5 35q0 17 .5 35t2.5 37Zm211 0h161q5-19 7-36.5t2-35.5q0-18-2-36t-7-36H622q2 19 2.5 37.5t.5 36.5q0 18-.5 35.5T622-408Zm-9-216h143q-29-57-77-97.5T571-779q14 30 24.5 69t17.5 86Zm-193 0h120q-9-55-26-101.5T480-789q-17 17-34 63.5T420-624Zm-216 0h143q7-47 17.5-86t24.5-69q-60 17-108 57.5T204-624Z"/></g>""");

    /// <summary>Material Symbols <c>tune</c>.</summary>
    public static readonly Glyph Tune =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M456-144v-240h72v84h288v72H528v84h-72Zm-312-84v-72h240v72H144Zm144-132v-84H144v-72h144v-84h72v240h-72Zm144-84v-72h384v72H432Zm144-132v-240h72v84h168v72H648v84h-72Zm-432-84v-72h384v72H144Z"/></g>""");

    /// <summary>Material Symbols <c>book_2</c>.</summary>
    public static readonly Glyph Directory =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M324-96q-54.69 0-93.34-38.66Q192-173.31 192-228v-504q0-54.69 38.66-93.34Q269.31-864 324-864h444v575q-25 0-42.5 17.91t-17.5 43.5q0 25.59 17.5 43.09Q743-167 768-167v71H324Zm-60-250q14-7 28.5-10.5T324-360h12v-432h-12q-25 0-42.5 17.5T264-732v386Zm144-14h288v-432H408v432Zm-144 14v-446 446Zm60 178h326q-7-14-10.5-28t-3.5-31.27q0-16.25 4-31.49Q644-274 651-288H324q-26 0-43 17.5T264-228q0 26 17 43t43 17Z"/></g>""");

    /// <summary>Material Symbols <c>groups</c>.</summary>
    public static readonly Glyph People =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M0-240v-59q0-51 45-80t123-29q15 0 30 1.5t30 4.5q-17 20-26.5 45t-9.5 50.56V-240H0Zm240 0v-61q0-27.86 14.5-50.93T293-387q44-22 91-33.5t95.53-11.5Q529-432 576-420.5t91 33.5q24 12 38.5 35.07T720-301v61H240Zm528 0v-67.37q0-26.95-9.5-50.79T732-402q17-3 31.5-4.5T792-408q78 0 123 29t45 80v59H768Zm-454-72h332q-7-17-59.5-32.5T480-360q-54 0-106.5 15.5T314-312ZM167.79-456Q138-456 117-477.03q-21-21.02-21-50.55Q96-558 117.03-579q21.02-21 50.55-21Q198-600 219-579.24t21 51.45Q240-498 219.24-477t-51.45 21Zm624 0Q762-456 741-477.03q-21-21.02-21-50.55Q720-558 741.03-579q21.02-21 50.55-21Q822-600 843-579.24t21 51.45Q864-498 843.24-477t-51.45 21ZM479.5-480q-49.5 0-84.5-35t-35-85q0-50 35-85t85-35q50 0 85 35t35 85.5q0 49.5-35 84.5t-85.5 35Zm.5-72q20.4 0 34.2-13.8Q528-579.6 528-600q0-20.4-13.8-34.2Q500.4-648 480-648q-20.4 0-34.2 13.8Q432-620.4 432-600q0 20.4 13.8 34.2Q459.6-552 480-552Zm0 240Zm0-288Z"/></g>""");

    /// <summary>Material Symbols <c>more_vert</c>.</summary>
    public static readonly Glyph More =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M479.79-192Q450-192 429-213.21t-21-51Q408-294 429.21-315t51-21Q510-336 531-314.79t21 51Q552-234 530.79-213t-51 21Zm0-216Q450-408 429-429.21t-21-51Q408-510 429.21-531t51-21Q510-552 531-530.79t21 51Q552-450 530.79-429t-51 21Zm0-216Q450-624 429-645.21t-21-51Q408-726 429.21-747t51-21Q510-768 531-746.79t21 51Q552-666 530.79-645t-51 21Z"/></g>""");

    /// <summary>Material Symbols <c>badge</c>.</summary>
    public static readonly Glyph Badge =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M168-96q-29.7 0-50.85-21.15Q96-138.3 96-168v-432q0-29.7 21.15-50.85Q138.3-672 168-672h216v-120q0-29.7 21.15-50.85Q426.3-864 456-864h48q29.7 0 50.85 21.15Q576-821.7 576-792v120h216q29.7 0 50.85 21.15Q864-629.7 864-600v432q0 29.7-21.15 50.85Q821.7-96 792-96H168Zm0-72h624v-432H576q0 30-21.15 51T504-528h-48q-29.7 0-50.85-21.15Q384-570.3 384-600H168v432Zm72-72h240v-23q0-17.63-9.5-32.67Q461-310.7 444-319q-20-8-40.5-12.5T360-336q-23 0-43.5 4.5T276-318.53q-17 7.53-26.5 22.66Q240-280.74 240-263v23Zm336-48h144v-72H576v72Zm-216-72q25 0 42.5-17.5T420-420q0-25-17.5-42.5T360-480q-25 0-42.5 17.5T300-420q0 25 17.5 42.5T360-360Zm216-48h144v-72H576v72ZM456-600h48v-192h-48v192Zm24 216Z"/></g>""");

    /// <summary>Material Symbols <c>key</c>.</summary>
    public static readonly Glyph Key =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M280-400q-33 0-56.5-23.5T200-480q0-33 23.5-56.5T280-560q33 0 56.5 23.5T360-480q0 33-23.5 56.5T280-400Zm0 160q-100 0-170-70T40-480q0-100 70-170t170-70q67 0 121.5 33t86.5 87h352l120 120-180 180-80-60-80 60-85-60h-47q-32 54-86.5 87T280-240Zm0-80q56 0 98.5-34t56.5-86h125l58 41 82-61 71 55 75-75-40-40H435q-14-52-56.5-86T280-640q-66 0-113 47t-47 113q0 66 47 113t113 47Z"/></g>""");

    /// <summary>Material Symbols <c>lock_open</c>.</summary>
    public static readonly Glyph LockOpen =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M264-96q-29.7 0-50.85-21.15Q192-138.3 192-168v-336q0-29.7 21.15-50.85Q234.3-576 264-576h276v-96q0-57.5 40.29-97.75t97.85-40.25q57.56 0 97.71 40.25T816-672h-72q0-27.5-19.32-46.75-19.31-19.25-46.82-19.25-27.51 0-46.68 19.25Q612-699.5 612-672v96h84q29.7 0 50.85 21.15Q768-533.7 768-504v336q0 29.7-21.15 50.85Q725.7-96 696-96H264Zm0-72h432v-336H264v336Zm216-108q30 0 51-21t21-51q0-30-21-51t-51-21q-30 0-51 21t-21 51q0 30 21 51t51 21ZM264-168v-336 336Z"/></g>""");

    /// <summary>Material Symbols <c>link</c>.</summary>
    public static readonly Glyph Link =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M440-280H280q-83 0-141.5-58.5T80-480q0-83 58.5-141.5T280-680h160v80H280q-50 0-85 35t-35 85q0 50 35 85t85 35h160v80ZM320-440v-80h320v80H320Zm200 160v-80h160q50 0 85-35t35-85q0-50-35-85t-85-35H520v-80h160q83 0 141.5 58.5T880-480q0 83-58.5 141.5T680-280H520Z"/></g>""");

    /// <summary>Material Symbols <c>card_membership</c>.</summary>
    public static readonly Glyph Membership =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M168-432v72h624v-72H168Zm0-432h624q29.7 0 50.85 21.15Q864-821.7 864-792v432q0 29.7-21.15 50.85Q821.7-288 792-288H624v192l-144-96-144 96v-192H168q-29.7 0-50.85-21.15Q96-330.3 96-360v-432q0-29.7 21.15-50.85Q138.3-864 168-864Zm0 336h624v-264H168v264Zm0 168v-432 432Z"/></g>""");

    /// <summary>Material Symbols <c>hub</c>.</summary>
    public static readonly Glyph Relationship =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M240-48q-50 0-85-35t-35-85q0-50 35-85t85-35q14 0 28.06 3.55 14.05 3.54 26.94 9.45l55-69q-29-31-42.5-70.5T302-496l-76-23q-15 29-44 46t-62 17q-50 0-85-35T0-576q0-51 34.5-85.5T120-696q51 0 85.5 34.5T240-576v11l76 23q20-42 57-70t83-35v-75q-42-8-69-41t-27-77q0-50 35-85t85-35q50 0 85 35t35 85q0 44-27 77t-69 41v74.97Q551-641 587.5-613t56.5 71l76-23v-11q0-50 35-85t85-35q50 0 85 35t35 85q0 50-35 85t-85 35q-33 0-62-16.5T734-519l-76 23q8 42-5.5 81.5T610-344l55 69q13-6 27-9.5t28-3.5q50 0 85 35t35 85q0 50-35 85t-85 35q-50 0-85-35t-35-85q0-21 7.5-40.5T628-245l-55-68q-43 26-93 26t-93-26l-55 68q13 17 20.5 36.47Q360-189.05 360-168q0 50-35 85t-85 35ZM120-528q20.4 0 34.2-13.8Q168-555.6 168-576q0-20.4-13.8-34.2Q140.4-624 120-624q-20.4 0-34.2 13.8Q72-596.4 72-576q0 20.4 13.8 34.2Q99.6-528 120-528Zm120 408q20.4 0 34.2-13.8Q288-147.6 288-168q0-20.4-13.8-34.2Q260.4-216 240-216q-20.4 0-34.2 13.8Q192-188.4 192-168q0 20.4 13.8 34.2Q219.6-120 240-120Zm240-672q20.4 0 34.2-13.8Q528-819.6 528-840q0-20.4-13.8-34.2Q500.4-888 480-888q-20.4 0-34.2 13.8Q432-860.4 432-840q0 20.4 13.8 34.2Q459.6-792 480-792Zm0 432q45.36 0 76.68-31.32Q588-422.64 588-468q0-45.36-31.32-76.68Q525.36-576 480-576q-45.36 0-76.68 31.32Q372-513.36 372-468q0 45.36 31.32 76.68Q434.64-360 480-360Zm240 240q20.4 0 34.2-13.8Q768-147.6 768-168q0-20.4-13.8-34.2Q740.4-216 720-216q-20.4 0-34.2 13.8Q672-188.4 672-168q0 20.4 13.8 34.2Q699.6-120 720-120Zm120-408q20.4 0 34.2-13.8Q888-555.6 888-576q0-20.4-13.8-34.2Q860.4-624 840-624q-20.4 0-34.2 13.8Q792-596.4 792-576q0 20.4 13.8 34.2Q819.6-528 840-528ZM480-840ZM120-576Zm360 108Zm360-108ZM240-168Zm480 0Z"/></g>""");

    /// <summary>Material Symbols <c>arrow_drop_down</c>.</summary>
    public static readonly Glyph DropDown =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-384 288-576h384L480-384Z"/></g>""");

    /// <summary>Material Symbols <c>chevron_right</c>.</summary>
    public static readonly Glyph ChevronRight =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M504-480 320-664l56-56 240 240-240 240-56-56 184-184Z"/></g>""");

    /// <summary>Material Symbols <c>check_box</c>.</summary>
    public static readonly Glyph CheckBoxOn =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-144q-29.7 0-50.85-21.15Q144-186.3 144-216v-528q0-29.7 21.15-50.85Q186.3-816 216-816h528q29.7 0 50.85 21.15Q816-773.7 816-744v528q0 29.7-21.15 50.85Q773.7-144 744-144H216Zm197-206 267-267-51-51-216 216-102-102-51 51 153 153Z"/></g>""");

    /// <summary>Material Symbols <c>check_box_outline_blank</c>.</summary>
    public static readonly Glyph CheckBoxOff =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-144q-29.7 0-50.85-21.15Q144-186.3 144-216v-528q0-29.7 21.15-50.85Q186.3-816 216-816h528q29.7 0 50.85 21.15Q816-773.7 816-744v528q0 29.7-21.15 50.85Q773.7-144 744-144H216Zm0-72h528v-528H216v528Z"/></g>""");

    /// <summary>Material Symbols <c>indeterminate_check_box</c>.</summary>
    public static readonly Glyph CheckBoxSome =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M312-444h336v-72H312v72ZM216-144q-29.7 0-50.85-21.15Q144-186.3 144-216v-528q0-29.7 21.15-50.85Q186.3-816 216-816h528q29.7 0 50.85 21.15Q816-773.7 816-744v528q0 29.7-21.15 50.85Q773.7-144 744-144H216Zm0-72h528v-528H216v528Z"/></g>""");

    /// <summary>Material Symbols <c>apartment</c>.</summary>
    public static readonly Glyph Business =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M144-144v-528h144v-144h360v288h168v384H528v-144h-96v144H144Zm72-72h72v-72h-72v72Zm0-156h72v-72h-72v72Zm0-156h72v-72h-72v72Zm144 156h72v-72h-72v72Zm0-156h72v-72h-72v72Zm0-144h72v-72h-72v72Zm144 300h72v-72h-72v72Zm0-156h72v-72h-72v72Zm0-144h72v-72h-72v72Zm168 456h72v-72h-72v72Zm0-156h72v-72h-72v72Z"/></g>""");

    /// <summary>Material Symbols <c>account_balance</c>.</summary>
    public static readonly Glyph Accounting =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M200-280v-280h80v280h-80Zm240 0v-280h80v280h-80ZM80-120v-80h800v80H80Zm600-160v-280h80v280h-80ZM80-640v-80l400-200 400 200v80H80Zm178-80h444L480-830 258-720Z"/></g>""");

    /// <summary>Material Symbols <c>account_tree</c> — accounts as the structure they form,
    /// told apart from the <c>account_balance</c> that stands for accounting itself.</summary>
    public static readonly Glyph Accounts =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M624-144v-108H444v-384H336v108H96v-288h240v108h288v-108h240v288H624v-108H516v312h108v-108h240v288H624ZM168-744v144-144Zm528 384v144-144Zm0-384v144-144Zm0 144h96v-144h-96v144Zm0 384h96v-144h-96v144ZM168-600h96v-144h-96v144Z"/></g>""");

    /// <summary>Material Symbols <c>menu_book</c>.</summary>
    public static readonly Glyph Book =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M560-564v-68q33-14 67.5-21t72.5-7q26 0 51 4t49 10v64q-24-9-48.5-13.5T700-600q-38 0-73 9.5T560-564Zm0 220v-68q33-14 67.5-21t72.5-7q26 0 51 4t49 10v64q-24-9-48.5-13.5T700-380q-38 0-73 9t-67 27Zm0-110v-68q33-14 67.5-21t72.5-7q26 0 51 4t49 10v64q-24-9-48.5-13.5T700-490q-38 0-73 9.5T560-454ZM260-320q47 0 91.5 10.5T440-278v-394q-41-24-87-36t-93-12q-36 0-71.5 7T120-692v396q35-12 69.5-18t70.5-6Zm260 42q44-21 88.5-31.5T700-320q36 0 70.5 6t69.5 18v-396q-33-14-68.5-21t-71.5-7q-47 0-93 12t-87 36v394Zm-40 118q-48-38-104-59t-116-21q-42 0-82.5 11T100-198q-21 11-40.5-1T40-234v-482q0-11 5.5-21T62-752q46-24 96-36t102-12q58 0 113.5 15T480-740q51-30 106.5-45T700-800q52 0 102 12t96 36q11 5 16.5 15t5.5 21v482q0 23-19.5 35t-40.5 1q-37-20-77.5-31T700-240q-60 0-116 21t-104 59Z"/></g>""");

    /// <summary>Material Symbols <c>receipt_long</c>.</summary>
    public static readonly Glyph Receipt =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M240-80q-50 0-85-35t-35-85v-120h120v-560l60 60 60-60 60 60 60-60 60 60 60-60 60 60 60-60v680q0 50-35 85t-85 35H240Zm480-80q17 0 28.5-11.5T760-200v-560H320v440h360v120q0 17 11.5 28.5T720-160ZM360-600v-80h240v80H360Zm0 120v-80h240v80H360Zm320-120q-17 0-28.5-11.5T640-640q0-17 11.5-28.5T680-680q17 0 28.5 11.5T720-640q0 17-11.5 28.5T680-600Zm0 120q-17 0-28.5-11.5T640-520q0-17 11.5-28.5T680-560q17 0 28.5 11.5T720-520q0 17-11.5 28.5T680-480ZM240-160h360v-80H200v40q0 17 11.5 28.5T240-160Zm-40 0v-80 80Z"/></g>""");

    /// <summary>Material Symbols <c>shopping_cart</c> — the buying side of a trade, where
    /// <c>receipt_long</c> is the paper either side produces.</summary>
    public static readonly Glyph Cart =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M263.79-96Q234-96 213-117.21t-21-51Q192-198 213.21-219t51-21Q294-240 315-218.79t21 51Q336-138 314.79-117t-51 21Zm432 0Q666-96 645-117.21t-21-51Q624-198 645.21-219t51-21Q726-240 747-218.79t21 51Q768-138 746.79-117t-51 21ZM253-696l83 192h301l82-192H253Zm-31-72h570q14 0 20.5 11t1.5 23L702.63-476.14Q694-456 676.5-444T637-432H317l-42 72h493v72H276q-43 0-63.5-36.15-20.5-36.16.5-71.85l52-90-131-306H48v-72h133l41 96Zm114 264h301-301Z"/></g>""");

    /// <summary>Material Symbols <c>storefront</c> — the shop as the place it sells from, the
    /// mirror of <c>shopping_cart</c>.</summary>
    public static readonly Glyph Storefront =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M816-504v287.64q0 29.85-21.15 51.1Q773.7-144 744-144H216q-29.7 0-50.85-21.5Q144-187 144-216v-289q-33-23-41.5-59t.5-76l46-128q5-24 23-36t43.67-12h528.66q25.67 0 43.17 11t23.5 37l46 128q9 40 0 76t-41 60Zm-248-48q21 0 35.5-14t12.5-34l-24-144h-72v143.62Q520-580 534-566q14 14 34 14Zm-176.5 0q20.5 0 34.5-14t14-34.38V-744h-72l-24 144q-2 20 12.5 34t35 14ZM216-552q18 0 31.5-11t15.5-28l25-153h-72l-45 128q-8 23 6 43.5t39 20.5Zm528 0q25 0 39.5-20.5T790-616l-46-128h-72l25 153q2 17 16 28t31 11ZM216-216h528v-264q-25 0-47.5-9.5T656-519q-18 20-40.36 29.5T568-480q-25.18 0-47.09-10Q499-500 480-519q-17 19-40 29t-48 10q-25 0-47-8.5T304-519q-22.02 21.55-43.51 30.28Q239-480 216-480v264Zm528 0H216h.5-.71 528.28H743h1Z"/></g>""");

    /// <summary>Material Symbols <c>payments</c>.</summary>
    public static readonly Glyph Payments =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M560-440q-50 0-85-35t-35-85q0-50 35-85t85-35q50 0 85 35t35 85q0 50-35 85t-85 35ZM280-320q-33 0-56.5-23.5T200-400v-320q0-33 23.5-56.5T280-800h560q33 0 56.5 23.5T920-720v320q0 33-23.5 56.5T840-320H280Zm80-80h400q0-33 23.5-56.5T840-480v-160q-33 0-56.5-23.5T760-720H360q0 33-23.5 56.5T280-640v160q33 0 56.5 23.5T360-400Zm440 240H120q-33 0-56.5-23.5T40-240v-440h80v440h680v80ZM280-400v-320 320Z"/></g>""");

    /// <summary>Material Symbols <c>price_check</c> — money come IN, where <c>payments</c> is
    /// money going out.</summary>
    public static readonly Glyph Collect =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M336-360v-48h-96v-72h192v-72H288q-20.4 0-34.2-13.8Q240-579.6 240-600v-120q0-20.4 13.8-34.2Q267.6-768 288-768h48v-48h72v48h96v72H312v72h144q20.4 0 34.2 13.8Q504-596.4 504-576v120q0 20.4-13.8 34.2Q476.4-408 456-408h-48v48h-72Zm237 216L415-302l51-51 107 107 192-192 51 51-243 243Z"/></g>""");

    /// <summary>Material Symbols <c>account_balance_wallet</c> — what one party's running
    /// balance comes to.</summary>
    public static readonly Glyph Wallet =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-223v7-528 521Zm0 79q-29.7 0-50.85-21.15Q144-186.3 144-216v-528q0-29.7 21.15-50.85Q186.3-816 216-816h528q29.7 0 50.85 21.15Q816-773.7 816-744v98h-72v-98H216v528h528v-99h72v99q0 29.7-21.15 50.85Q773.7-144 744-144H216Zm288-144q-29.7 0-50.85-21.15Q432-330.3 432-360v-240q0-29.7 21.15-50.85Q474.3-672 504-672h288q29.7 0 50.85 21.15Q864-629.7 864-600v240q0 29.7-21.15 50.85Q821.7-288 792-288H504Zm288-72v-240H504v240h288Zm-144-60q25 0 42.5-17.5T708-480q0-25-17.5-42.5T648-540q-25 0-42.5 17.5T588-480q0 25 17.5 42.5T648-420Z"/></g>""");

    /// <summary>Material Symbols <c>credit_card</c>.</summary>
    public static readonly Glyph Card =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M880-720v480q0 33-23.5 56.5T800-160H160q-33 0-56.5-23.5T80-240v-480q0-33 23.5-56.5T160-800h640q33 0 56.5 23.5T880-720ZM160-670h640v-50H160v50Zm0 190v240h640v-240H160Zm0 240v-480 480Z"/></g>""");

    /// <summary>Material Symbols <c>point_of_sale</c>.</summary>
    public static readonly Glyph Till =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M288-648q-29.7 0-50.85-21.19Q216-690.37 216-720.12v-72.13Q216-822 237.15-843T288-864h384q29.7 0 50.85 21.19Q744-821.63 744-791.88v72.13Q744-690 722.85-669T672-648H288Zm0-72h384v-72H288v72ZM168-96q-29.7 0-50.85-21.15Q96-138.3 96-168v-24h768v24q0 29.7-21.15 50.85Q821.7-96 792-96H168ZM96-240l149-318q8.8-19.66 26.4-30.83Q289-600 309.82-600h340.36q20.82 0 38.42 11.17T715-558l149 318H96Zm264-72h24q9.6 0 16.8-7.2 7.2-7.2 7.2-16.8 0-9.6-7.2-16.8-7.2-7.2-16.8-7.2h-24q-9.6 0-16.8 7.2-7.2 7.2-7.2 16.8 0 9.6 7.2 16.8 7.2 7.2 16.8 7.2Zm0-84h24q9.6 0 16.8-7.2 7.2-7.2 7.2-16.8 0-9.6-7.2-16.8-7.2-7.2-16.8-7.2h-24q-9.6 0-16.8 7.2-7.2 7.2-7.2 16.8 0 9.6 7.2 16.8 7.2 7.2 16.8 7.2Zm0-84h24q9.6 0 16.8-7.2 7.2-7.2 7.2-16.8 0-9.6-7.2-16.8-7.2-7.2-16.8-7.2h-24q-9.6 0-16.8 7.2-7.2 7.2-7.2 16.8 0 9.6 7.2 16.8 7.2 7.2 16.8 7.2Zm108 168h24q9.6 0 16.8-7.2 7.2-7.2 7.2-16.8 0-9.6-7.2-16.8-7.2-7.2-16.8-7.2h-24q-9.6 0-16.8 7.2-7.2 7.2-7.2 16.8 0 9.6 7.2 16.8 7.2 7.2 16.8 7.2Zm0-84h24q9.6 0 16.8-7.2 7.2-7.2 7.2-16.8 0-9.6-7.2-16.8-7.2-7.2-16.8-7.2h-24q-9.6 0-16.8 7.2-7.2 7.2-7.2 16.8 0 9.6 7.2 16.8 7.2 7.2 16.8 7.2Zm0-84h24q9.6 0 16.8-7.2 7.2-7.2 7.2-16.8 0-9.6-7.2-16.8-7.2-7.2-16.8-7.2h-24q-9.6 0-16.8 7.2-7.2 7.2-7.2 16.8 0 9.6 7.2 16.8 7.2 7.2 16.8 7.2Zm108 168h24q9.6 0 16.8-7.2 7.2-7.2 7.2-16.8 0-9.6-7.2-16.8-7.2-7.2-16.8-7.2h-24q-9.6 0-16.8 7.2-7.2 7.2-7.2 16.8 0 9.6 7.2 16.8 7.2 7.2 16.8 7.2Zm0-84h24q9.6 0 16.8-7.2 7.2-7.2 7.2-16.8 0-9.6-7.2-16.8-7.2-7.2-16.8-7.2h-24q-9.6 0-16.8 7.2-7.2 7.2-7.2 16.8 0 9.6 7.2 16.8 7.2 7.2 16.8 7.2Zm0-84h24q9.6 0 16.8-7.2 7.2-7.2 7.2-16.8 0-9.6-7.2-16.8-7.2-7.2-16.8-7.2h-24q-9.6 0-16.8 7.2-7.2 7.2-7.2 16.8 0 9.6 7.2 16.8 7.2 7.2 16.8 7.2Z"/></g>""");

    /// <summary>Material Symbols <c>currency_exchange</c>.</summary>
    public static readonly Glyph Exchange =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-48q-113 0-207.5-52.5T120-241v121H48v-240h240v72H175q48 76 128 122t177 46q75 0 140.5-28.5t114-77q48.5-48.5 77-114T840-480h72q0 90-34 168.5t-92.5 137Q727-116 648.5-82T480-48Zm-33-168v-48q-21-5-58.5-27T333-376l63-26q2 6 20 42.5t70 36.5q26 0 50.5-14.5T561-384q0-27-20.5-43.5T475-460q-31-11-78.5-35.5T349-585q0-3 13-49t86-62v-48h66v47q53 9 74.5 40t25.5 44l-59 25q-3-10-19-30t-53-20q-20 0-44 11.5T415-586q0 27 24.5 41t75.5 31q67 23 89.5 56.5T627-384q0 37-15 60t-34.5 36.5Q558-274 539.5-269t-26.5 6v47h-66ZM48-480q0-90 34-168.5t92.5-137Q233-844 311.5-878T480-912q113 0 207.5 52.5T840-719v-121h72v240H672v-72h113q-48-76-128-122t-177-46q-75 0-140.5 28.5t-114 77q-48.5 48.5-77 114T120-480H48Z"/></g>""");

    /// <summary>Material Symbols <c>inventory_2</c>.</summary>
    public static readonly Glyph Products =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M200-80q-33 0-56.5-23.5T120-160v-451q-18-11-29-28.5T80-680v-120q0-33 23.5-56.5T160-880h640q33 0 56.5 23.5T880-800v120q0 23-11 40.5T840-611v451q0 33-23.5 56.5T760-80H200Zm0-520v440h560v-440H200Zm-40-80h640v-120H160v120Zm200 280h240v-80H360v80Zm120 20Z"/></g>""");

    /// <summary>Material Symbols <c>category</c>.</summary>
    public static readonly Glyph Catalog =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="m260-520 220-360 220 360H260ZM700-80q-75 0-127.5-52.5T520-260q0-75 52.5-127.5T700-440q75 0 127.5 52.5T880-260q0 75-52.5 127.5T700-80Zm-580-20v-320h320v320H120Zm580-60q42 0 71-29t29-71q0-42-29-71t-71-29q-42 0-71 29t-29 71q0 42 29 71t71 29Zm-500-20h160v-160H200v160Zm202-420h156l-78-126-78 126Zm78 0ZM360-340Zm340 80Z"/></g>""");

    /// <summary>Material Symbols <c>label</c> — the name a thing is sold under, where
    /// <c>category</c> is the shelf it sits on.</summary>
    public static readonly Glyph Tag =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M816-480 646-267q-11 13-25.64 20-14.63 7-31.36 7H264q-29.7 0-50.85-21.15Q192-282.3 192-312v-336q0-29.7 21.15-50.85Q234.3-720 264-720h325q16.73 0 31.36 7Q635-706 646-693l170 213Zm-92 0L589-648H264v336h325l135-168Zm-460 0v168-336 168Z"/></g>""");

    /// <summary>Material Symbols <c>file_copy</c> — the form a document is stamped out of,
    /// which no single <c>description</c> page says.</summary>
    public static readonly Glyph Template =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M744-192H312q-29 0-50.5-21.5T240-264v-576q0-29 21.5-50.5T312-912h312l192 192v456q0 29-21.5 50.5T744-192ZM576-672v-168H312v576h432v-408H576ZM168-48q-29 0-50.5-21.5T96-120v-552h72v552h456v72H168Zm144-792v195-195 576-576Z"/></g>""");

    /// <summary>Material Symbols <c>person</c>.</summary>
    public static readonly Glyph Person =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-480q-66 0-113-47t-47-113q0-66 47-113t113-47q66 0 113 47t47 113q0 66-47 113t-113 47ZM192-192v-96q0-38 19-63t49-38q57-24 111.5-36T480-437q57 0 111 12.5T703-389q31 13 50 37.5t19 63.5v96H192Zm72-72h432v-24q0-16-9.5-30T663-339q-54-24-96-33t-87-9q-45 0-88 9t-96 33q-13 6-22.5 20t-9.5 30v24Zm216-288q33 0 56.5-23.5T560-640q0-33-23.5-56.5T480-720q-33 0-56.5 23.5T400-640q0 33 23.5 56.5T480-552Zm0-88Zm0 376Z"/></g>""");

    /// <summary>Material Symbols <c>person_add</c>.</summary>
    public static readonly Glyph PersonAdd =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M708-432v-84h-84v-72h84v-84h72v84h84v72h-84v84h-72Zm-426-90q-42-42-42-102t42-102q42-42 102-42t102 42q42 42 42 102t-42 102q-42 42-102 42t-102-42ZM96-192v-92q0-25.78 12.5-47.39T143-366q55-32 116-49t125-17q64 0 125 17t116 49q22 13 34.5 34.61T672-284v92H96Zm72-72h432v-20q0-6.47-3.03-11.76-3.02-5.3-7.97-8.24-47-27-99-41.5T384-360q-54 0-106 14.5T179-304q-4.95 2.94-7.98 8.24Q168-290.47 168-284v20Zm267-309.21q21-21.21 21-51T434.79-675q-21.21-21-51-21T333-674.79q-21 21.21-21 51T333.21-573q21.21 21 51 21T435-573.21ZM384-625Zm0 361Z"/></g>""");

    /// <summary>Material Symbols <c>print</c>.</summary>
    public static readonly Glyph Print =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M648-624v-96H312v96h-72v-168h480v168h-72ZM726-480q17 0 28.5-11.5T766-520q0-17-11.5-28.5T726-560q-17 0-28.5 11.5T686-520q0 17 11.5 28.5T726-480Zm-102 264v-144H336v144h288Zm72 72H264v-144H120v-192q0-31 21-51.5t51-20.5h576q31 0 51.5 20.5T840-480v192H696v144ZM768-360v-120q0-11-6.5-17.5T744-504H216q-11 0-17.5 6.5T192-480v120h72v-72h432v72h72Z"/></g>""");

    /// <summary>Material Symbols <c>logout</c>.</summary>
    public static readonly Glyph Logout =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M204-192q-29 0-50.5-21.5T132-264v-432q0-29 21.5-50.5T204-768h264v72H204v432h264v72H204Zm444-168-51-51 75-75H360v-72h312l-75-75 51-51 168 168-168 168Z"/></g>""");

    /// <summary>Material Symbols <c>undo</c>.</summary>
    public static readonly Glyph Undo =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M280-200v-72h284q63 0 109.5-40.5T720-406q0-63-46.5-104.5T564-552H312l104 104-56 56-208-208 208-208 56 56-104 104h252q97 0 165.5 63.5T780-406q0 90-68.5 148T508-200H280Z"/></g>""");

    /// <summary>Material Symbols <c>calendar_month</c>.</summary>
    public static readonly Glyph Calendar =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-96q-29.7 0-50.85-21.15Q144-138.3 144-168v-528q0-29.7 21.15-50.85Q186.3-768 216-768h72v-72h72v72h240v-72h72v72h72q29.7 0 50.85 21.15Q816-725.7 816-696v528q0 29.7-21.15 50.85Q773.7-96 744-96H216Zm0-72h528v-384H216v384Zm0-456h528v-72H216v72ZM336-408q15 0 25.5-10.5T372-444q0-15-10.5-25.5T336-480q-15 0-25.5 10.5T300-444q0 15 10.5 25.5T336-408Zm144 0q15 0 25.5-10.5T516-444q0-15-10.5-25.5T480-480q-15 0-25.5 10.5T444-444q0 15 10.5 25.5T480-408Zm144 0q15 0 25.5-10.5T660-444q0-15-10.5-25.5T624-480q-15 0-25.5 10.5T588-444q0 15 10.5 25.5T624-408ZM336-240q15 0 25.5-10.5T372-276q0-15-10.5-25.5T336-312q-15 0-25.5 10.5T300-276q0 15 10.5 25.5T336-240Zm144 0q15 0 25.5-10.5T516-276q0-15-10.5-25.5T480-312q-15 0-25.5 10.5T444-276q0 15 10.5 25.5T480-240Zm144 0q15 0 25.5-10.5T660-276q0-15-10.5-25.5T624-312q-15 0-25.5 10.5T588-276q0 15 10.5 25.5T624-240Z"/></g>""");

    /// <summary>Material Symbols <c>schedule</c>.</summary>
    public static readonly Glyph Schedule =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-96q-79 0-149-30t-122.5-82.5Q156-261 126-331T96-480q0-80 30-149.5t82.5-122Q261-804 331-834t149-30q80 0 149.5 30t122 82.5Q804-699 834-629.5T864-480q0 79-30 149t-82.5 122.5Q699-156 629.5-126T480-96Zm0-72q129 0 220.5-91.5T792-480q0-129-91.5-220.5T480-792q-129 0-220.5 91.5T168-480q0 129 91.5 220.5T480-168ZM610-286 661-337 516-482v-186h-72v216L610-286Z"/></g>""");

    /// <summary>Material Symbols <c>event_upcoming</c> — the days ahead, told apart from
    /// Calendar (the agenda itself) and Today (the day on screen).</summary>
    public static readonly Glyph Upcoming =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M576-96v-72h168v-360H216v216h-72v-384q0-29 21.15-50.5T216-768h72v-96h72v96h240v-96h72v96h72q29 0 50.5 21.5T816-696v528q0 29-21.5 50.5T744-96H576ZM363-48l-51-51 56-57H96v-72h272l-56-57 51-51 141 144L363-48ZM216-600h528v-96H216v96Zm0 0v-96 96Z"/></g>""");

    /// <summary>Material Symbols <c>today</c>.</summary>
    public static readonly Glyph Today =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M384.23-264Q344-264 316-291.77q-28-27.78-28-68Q288-400 315.77-428q27.78-28 68-28Q424-456 452-428.23q28 27.78 28 68Q480-320 452.23-292q-27.78 28-68 28ZM216-96q-29.7 0-50.85-21.5Q144-139 144-168v-528q0-29 21.15-50.5T216-768h72v-96h72v96h240v-96h72v96h72q29.7 0 50.85 21.5Q816-725 816-696v528q0 29-21.15 50.5T744-96H216Zm0-72h528v-360H216v360Zm0-432h528v-96H216v96Zm0 0v-96 96Z"/></g>""");

    /// <summary>Material Symbols <c>download</c>.</summary>
    public static readonly Glyph Download =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-336 288-528l51-51 105 105v-342h72v342l105-105 51 51-192 192ZM216-192q-29.7 0-50.85-21.15Q144-234.3 144-264v-120h72v120h528v-120h72v120q0 29.7-21.15 50.85Q773.7-192 744-192H216Z"/></g>""");

    /// <summary>Material Symbols <c>arrow_upward</c>.</summary>
    public static readonly Glyph MoveUp =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M440-160v-487L216-423l-56-57 320-320 320 320-56 57-224-224v487h-80Z"/></g>""");

    /// <summary>Material Symbols <c>arrow_downward</c>.</summary>
    public static readonly Glyph MoveDown =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M440-800v487L216-537l-56 57 320 320 320-320-56-57-224 224v-487h-80Z"/></g>""");

    /// <summary>Material Symbols <c>star</c>, filled.</summary>
    public static readonly Glyph Star =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="m233-120 65-281L80-590l288-25 112-265 112 265 288 25-218 189 65 281-247-149-247 149Z"/></g>""");

    /// <summary>Material Symbols <c>location_on</c>.</summary>
    public static readonly Glyph Location =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-480q33 0 56.5-23.5T560-560q0-33-23.5-56.5T480-640q-33 0-56.5 23.5T400-560q0 33 23.5 56.5T480-480Zm0 294q122-112 181-203.5T720-552q0-109-69.5-178.5T480-800q-101 0-170.5 69.5T240-552q0 71 59 162.5T480-186Zm0 106Q319-217 239.5-334.5T160-552q0-150 96.5-239T480-880q127 0 223.5 89T800-552q0 100-79.5 217.5T480-80Zm0-480Z"/></g>""");

    /// <summary>Material Symbols <c>mail</c>.</summary>
    public static readonly Glyph Mail =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M168-192q-29.7 0-50.85-21.16Q96-234.32 96-264.04v-432.24Q96-726 117.15-747T168-768h624q29.7 0 50.85 21.16Q864-725.68 864-695.96v432.24Q864-234 842.85-213T792-192H168Zm312-240L168-611v347h624v-347L480-432Zm0-85 312-179H168l312 179Zm-312-94v-85 432-347Z"/></g>""");

    /// <summary>Material Symbols <c>send</c>.</summary>
    public static readonly Glyph Send =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M144-192v-576l720 288-720 288Zm72-107 454-181-454-181v125l229 56-229 56v125Zm0 0v-362 362Z"/></g>""");

    /// <summary>Material Symbols <c>open_in_new</c> — opens somewhere else, not a plain link.</summary>
    public static readonly Glyph OpenExternal =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M216-120q-29.7 0-50.85-21.15Q144-162.3 144-192v-576q0-29.7 21.15-50.85Q186.3-840 216-840h264v72H216v576h576v-264h72v264q0 29.7-21.15 50.85Q821.7-120 792-120H216Zm171-192-51-51 357-357H576v-72h240v240h-72v-117L387-312Z"/></g>""");

    /// <summary>Material Symbols <c>phone_in_talk</c>.</summary>
    public static readonly Glyph Call =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M792-480q0-130-91-221t-221-91v-72q80 0 149.5 30t122 82.5Q804-699 834-629.5T864-480h-72Zm-144 0q0-70-49-119t-119-49v-72q100 0 170 70t70 170h-72Zm150 384q-119 0-238.5-58T343-343Q244-442 186-561.5T128-800q0-19.71 13.5-33.86Q155-848 176-848h140q15 0 26 10t15 26l27 126q2 14-.5 25.5T374-641L268-533q56 93 123.5 160T545-259l104-107q9-10 21-14t24-2l119 26q16 4 25.5 15.5T848-314v138q0 21-14.15 34.5T800-96ZM234-600l70-70-17-58h-83q3 34 10 66.5t20 61.5Zm361 358q30 14 63.5 21t68.5 10v-83l-59-13-73 65ZM234-600Zm361 358Z"/></g>""");

    /// <summary>Material Symbols <c>description</c>.</summary>
    public static readonly Glyph Note =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M336-240h288v-72H336v72Zm0-144h288v-72H336v72ZM263.72-96Q234-96 213-117.15T192-168v-624q0-29.7 21.15-50.85Q234.3-864 264-864h312l192 192v504q0 29.7-21.16 50.85Q725.68-96 695.96-96H263.72ZM528-624v-168H264v624h432v-456H528ZM264-792v189-189 624-624Z"/></g>""");

    /// <summary>Material Symbols <c>edit_note</c>.</summary>
    public static readonly Glyph Amend =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M192-396v-72h288v72H192Zm0-150v-72h432v72H192Zm0-150v-72h432v72H192Zm336 504v-113l210-209q7.26-7.41 16.13-10.71Q763-528 771.76-528q9.55 0 18.31 3.5Q798.83-521 806-514l44 45q6.59 7.26 10.29 16.13Q864-444 864-435.24t-3.29 17.92q-3.3 9.15-10.71 16.32L641-192H528Zm288-243-45-45 45 45ZM576-240h45l115-115-22-23-22-22-116 115v45Zm138-138-22-22 44 45-22-23Z"/></g>""");

    /// <summary>Material Symbols <c>stethoscope</c>.</summary>
    public static readonly Glyph Medical =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M540-81q-112 0-186-78.5T280-347v-35q-85-11-142.5-75.71T80-610v-230h120v-40h60v140h-60v-40h-60v169.68q0 71.32 49.5 120.82T310-440q71 0 120.5-49.5T480-610.32V-780h-60v40h-60v-140h60v40h120v230q0 87.58-57.5 152.29T340-382v35q0 85 56.5 145.5T540-141q81 0 140.5-60.15T740-347.23V-424q-35-10-57.5-39T660-530q0-45.83 32.12-77.92 32.12-32.08 78-32.08T848-607.92q32 32.09 32 77.92 0 38-22.5 67T800-424v77q0 111-76.5 188.5T540-81Zm265.5-413.32q14.5-14.33 14.5-35.5 0-21.18-14.32-35.68-14.33-14.5-35.5-14.5-21.18 0-35.68 14.32-14.5 14.33-14.5 35.5 0 21.18 14.32 35.68 14.33 14.5 35.5 14.5 21.18 0 35.68-14.32ZM770-530Z"/></g>""");

    /// <summary>Material Symbols <c>health_and_safety</c>.</summary>
    public static readonly Glyph Coverage =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M420-340h120v-100h100v-120H540v-100H420v100H320v120h100v100Zm60 260q-139-35-229.5-159.5T160-516v-244l320-120 320 120v244q0 152-90.5 276.5T480-80Zm0-84q104-33 172-132t68-220v-189l-240-90-240 90v189q0 121 68 220t172 132Zm0-316Z"/></g>""");

    /// <summary>Material Symbols <c>extension</c>.</summary>
    public static readonly Glyph Integration =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M352-120H200q-33 0-56.5-23.5T120-200v-152q48 0 84-30.5t36-77.5q0-47-36-77.5T120-568v-152q0-33 23.5-56.5T200-800h160q0-42 29-71t71-29q42 0 71 29t29 71h160q33 0 56.5 23.5T800-720v160q42 0 71 29t29 71q0 42-29 71t-71 29v160q0 33-23.5 56.5T720-120H568q0-50-31.5-85T460-240q-45 0-76.5 35T352-120Zm-152-80h85q24-66 77-93t98-27q45 0 98 27t77 93h85v-240h80q8 0 14-6t6-14q0-8-6-14t-14-6h-80v-240H480v-80q0-8-6-14t-14-6q-8 0-14 6t-6 14v80H200v88q54 20 87 67t33 105q0 57-33 104t-87 68v88Zm260-260Z"/></g>""");

    /// <summary>Material Symbols <c>cloud</c>.</summary>
    public static readonly Glyph Storage =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M264-216q-70 0-119-49t-49-119q0-64 41.5-111.5T240-553q17-71 73-119t135-48q88 0 149 61.5T658-509q69-3 117.5 42T824-355q0 58-40.5 98.5T685-216H264Zm0-72h421q29 0 49-20t20-49q0-29-20-49t-49-20h-61v-72q0-58-41-99t-99-41q-58 0-99 41t-41 99H264q-40 0-68 28t-28 68q0 40 28 68t68 28Zm216-192Z"/></g>""");

    /// <summary>Material Symbols <c>sync</c>.</summary>
    public static readonly Glyph Sync =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M167-160v-60h130l-15-12q-64-51-93-111t-29-134q0-106 62.5-190.5T387-784v62q-75 29-121 96.5T220-477q0 63 23.5 109.5T307-287l30 21v-124h60v230H167Zm407-15v-63q76-29 121-96.5T740-483q0-48-23.5-97.5T655-668l-29-26v124h-60v-230h230v60H665l15 14q60 56 90 120t30 123q0 106-62 191T574-175Z"/></g>""");

    /// <summary>Material Symbols <c>warning</c>.</summary>
    public static readonly Glyph Warning =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="m40-120 440-760 440 760H40Zm104-60h672L480-760 144-180Zm361.5-65.68q8.5-8.67 8.5-21.5 0-12.82-8.68-21.32-8.67-8.5-21.5-8.5-12.82 0-21.32 8.68-8.5 8.67-8.5 21.5 0 12.82 8.68 21.32 8.67 8.5 21.5 8.5 12.82 0 21.32-8.68ZM454-348h60v-224h-60v224Zm26-122Z"/></g>""");

    /// <summary>Material Symbols <c>help</c>. The circled question every field's help and the
    /// door to the guide share.</summary>
    public static readonly Glyph Help =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M514-254q14-14 14-34t-14-34q-14-14-34-14t-34 14q-14 14-14 34t14 34q14 14 34 14t34-14Zm-70-139h73q0-37 6.5-52.5T555-485q35-34 48.5-58t13.5-53q0-55-37.5-89.5T484-720q-51 0-88.5 27T343-620l65 27q9-28 28.5-43.5T482-652q28 0 46 16t18 42q0 23-15.5 41T496-518q-35 32-43.5 52.5T444-393Zm36 297q-79 0-149-30t-122.5-82.5Q156-261 126-331T96-480q0-80 30-149.5t82.5-122Q261-804 331-834t149-30q80 0 149.5 30t122 82.5Q804-699 834-629.5T864-480q0 79-30 149t-82.5 122.5Q699-156 629.5-126T480-96Zm0-72q130 0 221-91t91-221q0-130-91-221t-221-91q-130 0-221 91t-91 221q0 130 91 221t221 91Zm0-312Z"/></g>""");

    /// <summary>Material Symbols <c>confirmation_number</c>.</summary>
    public static readonly Glyph Tickets =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M480-280q17 0 28.5-11.5T520-320q0-17-11.5-28.5T480-360q-17 0-28.5 11.5T440-320q0 17 11.5 28.5T480-280Zm0-160q17 0 28.5-11.5T520-480q0-17-11.5-28.5T480-520q-17 0-28.5 11.5T440-480q0 17 11.5 28.5T480-440Zm0-160q17 0 28.5-11.5T520-640q0-17-11.5-28.5T480-680q-17 0-28.5 11.5T440-640q0 17 11.5 28.5T480-600Zm320 440H160q-33 0-56.5-23.5T80-240v-160q33 0 56.5-23.5T160-480q0-33-23.5-56.5T80-560v-160q0-33 23.5-56.5T160-800h640q33 0 56.5 23.5T880-720v160q-33 0-56.5 23.5T800-480q0 33 23.5 56.5T880-400v160q0 33-23.5 56.5T800-160Zm0-80v-102q-37-22-58.5-58.5T720-480q0-43 21.5-79.5T800-618v-102H160v102q37 22 58.5 58.5T240-480q0 43-21.5 79.5T160-342v102h640ZM480-480Z"/></g>""");

    /// <summary>Busy indicator. An icon like any other, so it fits every slot that takes one
    /// (button, adornment, table cell) — the spin comes from the ns-spin rules in ns-mud.css.</summary>
    // Hand-drawn rather than a Symbols glyph: this one is a shape the CSS animates, so it
    // stays in the 0 0 24 24 space the host svg already provides — no transform to undo.
    public static readonly Glyph Progress =
        new("""<circle class="ns-spin" cx="12" cy="12" r="9" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-dasharray="42 15"/>""");
}
