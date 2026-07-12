/// Bỏ dấu tiếng Việt + chuyển thường, để tìm kiếm không phân biệt dấu.
String removeDiacritics(String str) {
  var s = str.toLowerCase();
  const groups = {
    'a': 'àáảãạăằắẳẵặâầấẩẫậ',
    'e': 'èéẻẽẹêềếểễệ',
    'i': 'ìíỉĩị',
    'o': 'òóỏõọôồốổỗộơờớởỡợ',
    'u': 'ùúủũụưừứửữự',
    'y': 'ỳýỷỹỵ',
    'd': 'đ',
  };
  groups.forEach((base, chars) {
    for (final c in chars.split('')) {
      s = s.replaceAll(c, base);
    }
  });
  return s;
}
